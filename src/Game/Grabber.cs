using System.Collections.Generic;
using System.Linq;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Carries out a grab. Every take is one call the inventory screen also uses for a drag:
/// <see cref="Inventory.MoveItemToThis(Inventory, ItemDrop.ItemData, int, int, int)"/>, which
/// adds to the inventory and removes from the chest in the same step. For a chest another
/// player's game holds, MultiUserChest takes that call over and asks that game for the items;
/// what arrives is told once every answer is in.
/// </summary>
internal sealed class Grabber
{
    /// <summary>How long a grab waits for other players' games to answer before the next may start.</summary>
    private const float AnswerTimeout = 10f;

    private readonly Waiting _waiting = new(AnswerTimeout);
    private readonly Dictionary<(int X, int Y), (string Name, int Asked, int Before)> _asked = new();
    private readonly List<Arrival> _arrivals = new();
    private Inventory _waitingBag;
    private Player _waitingPlayer;

    private enum Taken
    {
        Moved,
        Asked,
        Changed,
        Refused,
    }

    public bool Busy => _waiting.Busy;

    /// <summary>What the last grab reported, for the self-test.</summary>
    internal IReadOnlyList<string> LastLines { get; private set; } = [];

    /// <summary>What the last answers from other players' chests reported, for the self-test.</summary>
    internal IReadOnlyList<string> LastArrivals { get; private set; } = [];

    /// <summary>Takes the last grab gave up waiting for, for the self-test.</summary>
    internal int LastUnanswered => _waiting.Unanswered.Count;

    public void Tick(float now)
    {
        if (!_waiting.Busy || _waitingBag == null)
        {
            return;
        }

        foreach (var slot in _waiting.Check(slot => MucLink.IsWaiting(_waitingBag, slot.X, slot.Y), now))
        {
            Settle(slot, answered: true);
        }

        if (_waiting.Busy)
        {
            return;
        }

        foreach (var slot in _waiting.Unanswered)
        {
            Settle(slot, answered: false);
        }

        if (_waiting.Unanswered.Count > 0)
        {
            Plugin.Log.LogWarning(System.FormattableString.Invariant($"{_waiting.Unanswered.Count} take(s) had no answer from the chest's game within {AnswerTimeout} s."));
        }

        LastArrivals = Report.Arrivals(_arrivals, Text.Shown, Settings.Messages.Value);
        Notice.Say(_waitingPlayer, LastArrivals);

        _arrivals.Clear();
        _asked.Clear();
    }

    public void Grab(Player player, Target target, float now)
    {
        if (_waiting.Busy)
        {
            Notice.Say(player, "Still waiting for the last rummage to arrive");
            return;
        }

        var scene = Scene.Read(player);
        var plan = Plan(target, scene);
        if (plan.Outcomes.Any(o => o.Short > 0))
        {
            scene.AddBarred(player);
            plan = Plan(target, scene);
        }

        var bag = player.GetInventory();
        ExplainNewStacks(plan, scene);
        var took = new Dictionary<string, int>();
        var asked = new Dictionary<string, int>();
        var waitingFor = new List<(int X, int Y)>();
        var touched = new HashSet<Container>();
        int changed = 0;
        int refused = 0;
        LastArrivals = [];

        foreach (var move in plan.Moves)
        {
            var chest = scene.Chests[move.SourceId];
            switch (Take(chest, bag, move, out int amount))
            {
                case Taken.Changed:
                    changed++;
                    continue;
                case Taken.Refused:
                    refused++;
                    continue;
                case Taken.Asked:
                    Add(asked, move.Name, amount);
                    waitingFor.Add((move.ToX, move.ToY));
                    _asked[(move.ToX, move.ToY)] = (move.Name, amount, CountAt(bag, move.ToX, move.ToY, move.Name));
                    break;
                default:
                    Add(took, move.Name, amount);
                    break;
            }

            touched.Add(chest);
        }

        _waiting.Start(waitingFor, now);
        _waitingBag = bag;
        _waitingPlayer = player;

        foreach (var chest in touched)
        {
            Highlighter.Light(chest, Settings.TakeGlow.Value);
        }

        var tooFar = Finder.FindFar(player, scene, Locate.SoughtFurther(plan));
        if (took.Count > 0 || asked.Count > 0)
        {
            Sound.Play(Sound.Cue.Take);
        }
        else if (Finder.LastFarLit.Count > 0)
        {
            Sound.Play(Sound.Cue.Find);
        }

        LastLines = Report.Lines(target.Label, plan, new Done(took, asked, changed, refused), Text.Shown, MucLink.Ready, Settings.Messages.Value, tooFar);
        Notice.Say(player, LastLines);
    }

    /// <summary>One of every item in the chests nearby the player has never carried, which teaches the game each of them.</summary>
    public void GrabNew(Player player, float now)
    {
        if (_waiting.Busy)
        {
            Notice.Say(player, "Still waiting for the last rummage to arrive");
            return;
        }

        var wants = NewItems.Wants(Scene.Read(player).Sources, player.IsKnownMaterial);
        if (wants.Count == 0)
        {
            LastLines = [NewItems.NothingNew];
            Notice.Say(player, LastLines);
            return;
        }

        Grab(player, Target.ForNew(wants.Select(w => w.Name)), now);
    }

    /// <summary>
    /// The plan for the target. A recipe that takes any one of its ingredients is planned once per
    /// ingredient, and the one carried already, else the first the chests make up in full, else
    /// the closest, is fetched.
    /// </summary>
    private static Plan Plan(Target target, Scene scene)
    {
        if (!target.AnyOf || target.Cost.Count < 2)
        {
            return Planner.Make(target.Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne);
        }

        var plans = target.Cost.Select(part => Planner.Make([target.Want(part)], scene.Sources, scene.Bag, CraftyLink.LeaveOne)).ToList();
        return plans[Choice.Best(plans)];
    }

    /// <summary>An answered take: what its slot gained of the item since it was asked.</summary>
    private void Settle((int X, int Y) slot, bool answered)
    {
        if (_asked.TryGetValue(slot, out var take))
        {
            int arrived = Mathf.Max(0, CountAt(_waitingBag, slot.X, slot.Y, take.Name) - take.Before);
            _arrivals.Add(new Arrival(take.Name, take.Asked, arrived, answered));
            _asked.Remove(slot);
        }
    }

    /// <summary>Makes one take, and says how it went and how many items it moved or asked for.</summary>
    private static Taken Take(Container chest, Inventory bag, Move move, out int amount)
    {
        amount = 0;
        var view = Scene.View(chest);
        var from = chest ? chest.GetInventory() : null;
        var item = from?.GetItemAt(move.FromX, move.FromY);
        if (!view || !view.IsValid() || item?.m_shared == null || item.m_shared.m_name != move.Name || item.m_stack < move.Amount)
        {
            return Taken.Changed;
        }

        var reach = Scene.ReachOf(chest, view);
        if (reach != move.Reach)
        {
            return Taken.Changed;
        }

        int before = item.m_stack;
        if (reach == Reach.Owned)
        {
            bag.MoveItemToThis(from, item, move.Amount, move.ToX, move.ToY);
            amount = before - item.m_stack;
            return amount > 0 ? Taken.Moved : Taken.Changed;
        }

        // Another game holds the chest. Hand the take over only when MultiUserChest will carry it,
        // because otherwise the call would change the chest here, where nothing saves it.
        if (reach != Reach.Routed || !MucLink.Knows(chest, item))
        {
            return Taken.Changed;
        }

        bag.MoveItemToThis(from, item, move.Amount, move.ToX, move.ToY);
        if (item.m_stack != before)
        {
            Plugin.Log.LogError($"A take from {chest.name} changed the chest in this game although another game holds it.");
            amount = before - item.m_stack;
            return Taken.Moved;
        }

        if (!MucLink.IsWaiting(bag, move.ToX, move.ToY))
        {
            return Taken.Refused;
        }

        amount = move.Amount;
        return Taken.Asked;
    }

    /// <summary>
    /// Logs a take planned into an empty slot while a stack of the same item in the inventory still
    /// has room after the grab, with what keeps the two apart: the game stacks only items of the same world
    /// level, and of the same quality for items with quality levels.
    /// </summary>
    private static void ExplainNewStacks(Plan plan, Scene scene)
    {
        var bag = scene.Bag;
        foreach (var move in plan.Moves)
        {
            if (bag.Stacks.Any(s => s.X == move.ToX && s.Y == move.ToY))
            {
                continue;
            }

            var open = bag.Stacks.FirstOrDefault(s => s.Name == move.Name
                && s.Count + plan.Moves.Where(m => m.ToX == s.X && m.ToY == s.Y).Sum(m => m.Amount) < s.MaxStack);
            if (open != null)
            {
                Plugin.Log.LogInfo($"{Text.Shown(move.Name)} goes to a new stack at ({move.ToX},{move.ToY}) beside the one at ({open.X},{open.Y}) "
                    + $"({open.Count}/{open.MaxStack}, world level {open.WorldLevel}, quality {open.Quality}); the chest's stack is {From(scene, move)}.");
            }
        }
    }

    private static string From(Scene scene, Move move)
    {
        var stack = scene.Sources.FirstOrDefault(s => s.Id == move.SourceId)?.Stacks.FirstOrDefault(s => s.X == move.FromX && s.Y == move.FromY);
        return stack == null ? "not known" : $"world level {stack.WorldLevel}, quality {stack.Quality}";
    }

    private static int CountAt(Inventory bag, int x, int y, string name)
    {
        var item = bag.GetItemAt(x, y);
        return item?.m_shared != null && item.m_shared.m_name == name ? item.m_stack : 0;
    }

    private static void Add(Dictionary<string, int> counts, string name, int amount)
        => counts[name] = (counts.TryGetValue(name, out int sum) ? sum : 0) + amount;

}
