using System.Collections.Generic;
using Rummage.Core;
using UnityEngine;

namespace Rummage.Game;

/// <summary>
/// Carries out a grab. Every take is one call the inventory screen also uses for a drag:
/// <see cref="Inventory.MoveItemToThis(Inventory, ItemDrop.ItemData, int, int, int)"/>, which
/// adds to the inventory and removes from the chest in the same step. For a chest another
/// player's game holds, MultiUserChest takes that call over and asks that game for the items.
/// </summary>
internal sealed class Grabber
{
    /// <summary>How long a grab waits for other players' games to answer before the next may start.</summary>
    private const float AnswerTimeout = 10f;

    private readonly Waiting _waiting = new(AnswerTimeout);
    private Inventory _waitingBag;

    public bool Busy => _waiting.Busy;

    public void Tick(float now)
    {
        if (!_waiting.Busy || _waitingBag == null)
        {
            return;
        }

        _waiting.Check(slot => MucLink.IsWaiting(_waitingBag, slot.X, slot.Y), now);
        if (!_waiting.Busy && _waiting.Unanswered.Count > 0)
        {
            Plugin.Log.LogWarning($"{_waiting.Unanswered.Count} take(s) had no answer from the chest's game within {AnswerTimeout} s.");
        }
    }

    public void Grab(Player player, Target target, float now)
    {
        if (_waiting.Busy)
        {
            Say(player, "Still waiting for the last rummage to arrive");
            return;
        }

        var scene = Scene.Read(player);
        var plan = Planner.Make(target.Wants, scene.Sources, scene.Bag, CraftyLink.LeaveOne);

        var bag = player.GetInventory();
        var taken = new Dictionary<string, int>();
        var waitingFor = new List<(int X, int Y)>();
        var touched = new HashSet<Container>();
        int skipped = 0;

        foreach (var move in plan.Moves)
        {
            var chest = scene.Chests[move.SourceId];
            int amount = Take(chest, bag, move, out bool answerLater);
            if (amount <= 0)
            {
                skipped++;
                continue;
            }

            taken[move.Name] = (taken.TryGetValue(move.Name, out int sum) ? sum : 0) + amount;
            touched.Add(chest);
            if (answerLater)
            {
                waitingFor.Add((move.ToX, move.ToY));
            }
        }

        _waiting.Start(waitingFor, now);
        _waitingBag = bag;

        foreach (var chest in touched)
        {
            Highlight(chest);
        }

        foreach (string line in Report.Lines(target.Label, plan, taken, skipped, Shown))
        {
            Say(player, line);
        }
    }

    /// <summary>Makes one take; returns how many items it moved or asked for, zero when it was not made.</summary>
    private static int Take(Container chest, Inventory bag, Move move, out bool answerLater)
    {
        answerLater = false;
        var view = Scene.View(chest);
        var from = chest ? chest.GetInventory() : null;
        var item = from?.GetItemAt(move.FromX, move.FromY);
        if (!view || !view.IsValid() || item?.m_shared == null || item.m_shared.m_name != move.Name || item.m_stack < move.Amount)
        {
            return 0;
        }

        var reach = Scene.ReachOf(chest, view);
        if (reach != move.Reach)
        {
            return 0;
        }

        int before = item.m_stack;
        if (reach == Reach.Owned)
        {
            bag.MoveItemToThis(from, item, move.Amount, move.ToX, move.ToY);
            return before - item.m_stack;
        }

        // Another game holds the chest. Hand the take over only when MultiUserChest will carry it,
        // because otherwise the call would change the chest here, where nothing saves it.
        if (reach != Reach.Routed || !MucLink.Knows(chest, item))
        {
            return 0;
        }

        bag.MoveItemToThis(from, item, move.Amount, move.ToX, move.ToY);
        if (item.m_stack != before)
        {
            Plugin.Log.LogError($"A take from {chest.name} changed the chest in this game although another game holds it.");
            return before - item.m_stack;
        }

        if (!MucLink.IsWaiting(bag, move.ToX, move.ToY))
        {
            return 0;
        }

        answerLater = true;
        return move.Amount;
    }

    private static void Highlight(Container chest)
    {
        var root = chest.m_rootObjectOverride ? chest.m_rootObjectOverride.gameObject : chest.gameObject;
        if (root.TryGetComponent(out WearNTear piece))
        {
            piece.Highlight();
        }
    }

    private static string Shown(string name) => Localization.instance != null ? Localization.instance.Localize(name) : name;

    private static void Say(Player player, string line) => player.Message(MessageHud.MessageType.TopLeft, line);
}
