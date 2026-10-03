using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Rummage.Core;
using Rummage.Game;
using UnityEngine;

namespace Rummage.SelfTest;

/// <summary>What the self-test found, written to the log as it goes.</summary>
internal sealed class Checks
{
    private readonly ManualLogSource _log;
    private readonly List<string> _failed = new();
    private string _part = string.Empty;
    private int _passed;
    private int _skipped;

    public Checks(ManualLogSource log)
    {
        _log = log;
    }

    public int Failed => _failed.Count;

    public void Start(string part)
    {
        _part = part;
        Write($"START {part}");
    }

    public void Check(bool held, string what, string found = null)
    {
        if (held)
        {
            _passed++;
            Write($"  PASS {what}");
        }
        else
        {
            Fail(what, found);
        }
    }

    public void Fail(string what, string found)
    {
        _failed.Add($"{_part}: {what}{(found == null ? string.Empty : $" ({found})")}");
        Write($"  FAIL {what}{(found == null ? string.Empty : $": {found}")}");
    }

    public void Note(string text) => Write($"  note {text}");

    public void Skip(string why)
    {
        _skipped++;
        Write($"  SKIP {why}");
    }

    public void Summary()
    {
        Write($"DONE {_passed} passed, {_failed.Count} failed, {_skipped} skipped");
        foreach (string failure in _failed)
        {
            Write($"  failed: {failure}");
        }
    }

    public void Write(string line) => _log.LogInfo($"[selftest] {line}");
}

/// <summary>
/// The self-test: each part uses Rummage as the key does, on chests of the test's own, and checks
/// what it did. After every grab, what the inventory and the test chests hold of each item
/// together must be what they held before: anything more is a duplicate, anything less a loss.
/// </summary>
internal sealed class Tests
{
    private static readonly string[] Items = [Site.Wood, Site.Stone, Site.Resin, Site.Arrows];

    private readonly Checks _checks;
    private Player _player;
    private Site _site;

    public Tests(ManualLogSource log)
    {
        _checks = new Checks(log);
    }

    private static Grabber Grabber => Plugin.Instance.Grabber;

    public IEnumerator Run()
    {
        _player = Player.m_localPlayer;
        _checks.Write("Rummage self-test. Stand still until DONE; your belongings are set aside in chests next to you and put back at the end.");
        _player.Message(MessageHud.MessageType.Center, "Rummage self-test running, stand still");

        Startup();
        if (!CraftyLink.Ready)
        {
            _checks.Summary();
            yield break;
        }

        _site = new Site(_player, _checks);
        bool built = Guarded("building the test site", _site.Build);
        if (built)
        {
            CraftyLink.OnlyChests = chest => _site.Sources.Contains(chest);
            yield return Until(() => _site.Sources.All(c => CraftyLink.NearbyChests(_player).Contains(c)), 6f);
            built = _site.Sources.All(c => CraftyLink.NearbyChests(_player).Contains(c));
            _checks.Check(built, "AzuCraftyBoxes lists the test chests", "it did not within 6 seconds");
        }

        if (built)
        {
            yield return Part("a build piece placed with the hammer, pressed twice", HammerPiece());
            yield return Part("Leave One Item", LeaveOne());
            yield return Part("chest rules", ChestRules());
            yield return Part("a full inventory with one part stack", FullInventory());
            yield return Part("a recipe in the crafting tab, pressed twice", Recipe());
            yield return Part("filling a stack", TopUp());
            yield return Part("the search box", SearchBox());
            yield return Part("a chest held by a game that never answers", NeverAnswers());
            yield return Part("a chest held by another player's game", AnotherPlayer());
        }

        _checks.Start("putting everything back");
        Guarded("putting everything back", _site.TearDown);
        _checks.Summary();
        _player.Message(MessageHud.MessageType.Center, _checks.Failed == 0 ? "Rummage self-test passed" : $"Rummage self-test: {_checks.Failed} failed, see the log");
    }

    private void Startup()
    {
        _checks.Start("startup");
        _checks.Check(CraftyLink.Ready, "AzuCraftyBoxes found and understood");
        if (Chainloader.PluginInfos.ContainsKey(MucLink.Guid))
        {
            _checks.Check(MucLink.Ready, "MultiUserChest found and understood");
        }
        else
        {
            _checks.Note("MultiUserChest is not loaded");
        }

        _checks.Note(Chainloader.PluginInfos.ContainsKey(CraftAmountLink.Guid) ? $"AzuAntiArthriticCrafting's craft amount reads {CraftAmountLink.Current()}" : "AzuAntiArthriticCrafting is not loaded");
        _checks.Note($"Leave One Item is {(CraftyLink.LeaveOne ? "on" : "off")}, range {CraftyLink.Range} m");
        foreach (var (type, method) in new[] { (typeof(TextInput), "IsVisible"), (typeof(InventoryGui), "Update"), (typeof(ZInput), "Internal_GetMouseScrollWheel") })
        {
            var info = Harmony.GetPatchInfo(AccessTools.Method(type, method));
            _checks.Check(info != null && info.Owners.Contains(Plugin.Guid), $"{type.Name}.{method} is patched");
        }
    }

    private IEnumerator HammerPiece()
    {
        var piece = ZNetScene.instance.GetPrefab("piece_workbench")?.GetComponent<Piece>();
        var hammer = Site.Make("Hammer", 1);
        _site.Bag.AddItem(hammer);
        _player.EquipItem(hammer, triggerEquipEffects: false);
        yield return Until(() => _player.InPlaceMode(), 3f);
        if (!_player.InPlaceMode() || !piece || !_player.SetSelectedPiece(piece))
        {
            _checks.Skip("the hammer could not be taken up with the workbench selected");
            yield break;
        }

        int cost = piece.m_resources.Where(r => r.m_resItem.m_itemData.m_shared.m_name == Site.Wood).Sum(r => r.m_amount);
        for (int press = 1; press <= 2; press++)
        {
            var target = Context.Find(_player, hovered: false);
            _checks.Check(target?.Kind == EntryKind.Piece && target.Shown == Text.Shown(piece.m_name), $"press {press}: the key is for the selected workbench", target?.Label ?? "nothing");
            _checks.Check(target?.Count == press, $"press {press}: it is for {press} of them", target?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (target == null)
            {
                yield break;
            }

            yield return Grab(target);
            _checks.Check(Site.Count(_site.Bag, Site.Wood) == cost * press, $"press {press}: the inventory holds wood for {press}", $"{Site.Count(_site.Bag, Site.Wood)} wood");
        }

        _site.PutHandsBack();
    }

    private IEnumerator LeaveOne()
    {
        yield return Grab(Target.ForItem(Site.Wood, "Wood", 1000));
        int expected = CraftyLink.LeaveOne ? 1 : 0;
        foreach (var chest in new[] { _site.Main, _site.Second })
        {
            _checks.Check(Site.Count(chest.GetInventory(), Site.Wood) == expected, $"a test chest keeps {expected} wood with Leave One Item {(CraftyLink.LeaveOne ? "on" : "off")}", $"{Site.Count(chest.GetInventory(), Site.Wood)} left");
        }

        if (CraftyLink.LeaveOne)
        {
            _checks.Check(Grabber.LastLines.Any(l => l.Contains("kept back, one in every chest")), "the report says what was kept back", string.Join(" | ", Grabber.LastLines));
        }
    }

    private IEnumerator ChestRules()
    {
        if (CraftyLink.MayTake("piece_chest_private", "Resin"))
        {
            _checks.Skip("AzuCraftyBoxes' rules let private chests give resin here, so there is no rule to see");
            yield break;
        }

        yield return Grab(Target.ForItem(Site.Resin, "Resin", 5));
        _checks.Check(Site.Count(_site.Bag, Site.Resin) == 0, "nothing came out of the private chest");
        _checks.Check(Site.Count(_site.Private.GetInventory(), Site.Resin) == 20, "the private chest still holds its resin");
        _checks.Check(Grabber.LastLines.Any(l => l.Contains("kept in by chest rules")), "the report names the chest rules", string.Join(" | ", Grabber.LastLines));
    }

    private IEnumerator FullInventory()
    {
        var (x, y) = _site.FreeSlot();
        _site.Bag.AddItem(Site.Make("Wood", 45), new Vector2i(x, y));
        for (var slot = _site.FreeSlot(); slot.X >= 0; slot = _site.FreeSlot())
        {
            _site.Bag.AddItem(Site.Make("Stone", 50), new Vector2i(slot.X, slot.Y));
        }

        yield return Grab(Target.ForItem(Site.Wood, "Wood", 10));
        _checks.Check(_site.Bag.GetItemAt(x, y)?.m_stack == 50, "the part stack is filled to 50", _site.Bag.GetItemAt(x, y)?.m_stack.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _checks.Check(Site.Count(_site.Bag, Site.Wood) == 50, "no wood landed anywhere else");
        _checks.Check(Grabber.LastLines.Any(l => l.Contains("no room left")), "the report says the inventory is full", string.Join(" | ", Grabber.LastLines));
    }

    private IEnumerator Recipe()
    {
        var gui = InventoryGui.instance;
        gui.Show(null);
        yield return Frames(10);
        AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel").Invoke(gui, [false]);
        var recipes = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes").GetValue(gui) as IList;
        int index = -1;
        Recipe recipe = null;
        for (int i = 0; recipes != null && i < recipes.Count; i++)
        {
            var pair = recipes[i];
            var candidate = pair.GetType().GetProperty("Recipe").GetValue(pair) as Recipe;
            if (candidate && candidate.m_item.m_itemData.m_shared.m_name == "$item_hammer" && pair.GetType().GetProperty("ItemData").GetValue(pair) == null)
            {
                (index, recipe) = (i, candidate);
                break;
            }
        }

        if (index < 0)
        {
            _checks.Skip("the hammer's recipe is not in the crafting tab here");
            gui.Hide();
            yield break;
        }

        AccessTools.Method(typeof(InventoryGui), "SetRecipe").Invoke(gui, [index, false]);
        int step = CraftAmountLink.Current();
        for (int press = 1; press <= 2; press++)
        {
            var target = Context.Find(_player, hovered: false);
            _checks.Check(target?.Kind == EntryKind.Recipe, $"press {press}: the key is for the selected recipe", target?.Label ?? "nothing");
            _checks.Check(target?.Count == step * press, $"press {press}: it is for {step * press} crafts", target?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (target == null)
            {
                break;
            }

            yield return Grab(target);
            foreach (var requirement in recipe.m_resources.Where(r => r.GetAmount(1) > 0))
            {
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int want = requirement.GetAmount(1) * step * press;
                _checks.Check(Site.Count(_site.Bag, name) >= Math.Min(want, _site.Total(name)), $"press {press}: the inventory holds {want} {Text.Shown(name)}", Site.Count(_site.Bag, name).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        gui.Hide();
    }

    private IEnumerator TopUp()
    {
        var (x, y) = _site.FreeSlot();
        _site.Bag.AddItem(Site.Make("ArrowWood", 23), new Vector2i(x, y));
        var held = _site.Bag.GetItemAt(x, y);
        yield return Grab(Target.ForTopUp(Scene.StackOf(held)));
        _checks.Check(_site.Bag.GetItemAt(x, y)?.m_stack == held.m_shared.m_maxStackSize, "the stack is full", _site.Bag.GetItemAt(x, y)?.m_stack.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _checks.Check(_site.Bag.GetAllItems().Count(i => i.m_shared.m_name == Site.Arrows) == 1, "no arrows landed in another slot");
    }

    private IEnumerator SearchBox()
    {
        var palette = Plugin.Instance.Palette;
        palette.Open(_player, null);
        _checks.Check(Palette.IsOpen && TextInput.IsVisible(), "while open, the game treats it as a text box");
        palette.Text = "40 wood";
        _checks.Check(palette.Results.Count > 0 && palette.Results[0].Key == Site.Wood, "\"40 wood\" puts wood first", palette.Results.Count > 0 ? palette.Results[0].Shown : "nothing");
        int before = Site.Count(_site.Bag, Site.Wood);
        palette.TakeChosen(_player);
        _checks.Check(!Palette.IsOpen && !TextInput.IsVisible(), "Enter closes it and gives the keys back");
        yield return Idle();
        _checks.Check(Site.Count(_site.Bag, Site.Wood) - before == 40, "40 wood arrived", (Site.Count(_site.Bag, Site.Wood) - before).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private IEnumerator NeverAnswers()
    {
        if (!MucLink.Ready)
        {
            _checks.Skip("MultiUserChest is not loaded or not understood");
            yield break;
        }

        CraftyLink.OnlyChests = chest => chest == _site.Main;
        var view = Scene.View(_site.Main);
        long nobody = 0x5EED_0000_0001L;
        view.GetZDO().SetOwner(nobody);
        _checks.Check(Scene.ReachOf(_site.Main, view) == Reach.Routed, "a chest another game holds goes through MultiUserChest");

        var before = Snapshot();
        Grabber.Grab(_player, Target.ForItem(Site.Wood, "Wood", 5), Time.time);
        _checks.Check(Site.Count(_site.Main.GetInventory(), Site.Wood) == 50, "the chest is not changed in this game");
        _checks.Check(Site.Count(_site.Bag, Site.Wood) == 0, "nothing arrives without an answer");
        _checks.Check(Grabber.Busy, "the next grab waits for the answer");
        yield return Idle(15f);
        _checks.Check(!Grabber.Busy && Grabber.LastUnanswered > 0, "the wait ends after its timeout and says so");
        Conserved(before);

        ReleaseMultiUserChestHold();
        yield return Until(() => view.IsOwner(), 5f);
        if (!view.IsOwner())
        {
            _checks.Note("the chest did not come back to this game by itself; claiming it");
            view.ClaimOwnership();
        }
    }

    private IEnumerator AnotherPlayer()
    {
        var other = Player.GetAllPlayers().FirstOrDefault(p => p && p != _player && Vector3.Distance(p.transform.position, _player.transform.position) < 30f);
        if (!other || !MucLink.Ready)
        {
            _checks.Skip(MucLink.Ready ? "no other player within 30 m; stand next to someone to cover this" : "MultiUserChest is not loaded or not understood");
            yield break;
        }

        CraftyLink.OnlyChests = chest => chest == _site.Second;
        long uid = other.GetComponent<ZNetView>().GetZDO().GetOwner();
        var view = Scene.View(_site.Second);
        var zdo = view.GetZDO();
        ZDOMan.instance.ForceSendZDO(uid, zdo.m_uid);
        zdo.SetOwner(uid);
        _checks.Note($"the second test chest is handed to {other.GetPlayerName()}'s game");
        yield return Frames(30);

        var before = Snapshot();
        Grabber.Grab(_player, Target.ForItem(Site.Wood, "Wood", 5), Time.time);
        yield return Idle(15f);
        _checks.Check(Grabber.LastUnanswered == 0, "their game answered");
        _checks.Check(Site.Count(_site.Bag, Site.Wood) == 5, "5 wood arrived", Site.Count(_site.Bag, Site.Wood).ToString(System.Globalization.CultureInfo.InvariantCulture));
        yield return Until(() => Site.Count(_site.Second.GetInventory(), Site.Wood) == 45, 8f);
        _checks.Check(Site.Count(_site.Second.GetInventory(), Site.Wood) == 45, "their game took 5 wood out of the chest", Site.Count(_site.Second.GetInventory(), Site.Wood).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Conserved(before);

        yield return Until(() => zdo.GetInt(ZDOVars.s_inUse) == 0, 5f);
        view.ClaimOwnership();
    }

    /// <summary>
    /// Runs a part on a fresh inventory and freshly filled chests, failing it rather than the test
    /// when it throws. What is made or lost is checked after every grab, as parts add items of
    /// their own to set a scene.
    /// </summary>
    private IEnumerator Part(string name, IEnumerator part)
    {
        _checks.Start(name);
        Guarded("getting ready", () =>
        {
            CraftyLink.OnlyChests = chest => _site.Sources.Contains(chest);
            _site.PutHandsBack();
            _site.ClearBag();
            _site.Refill();
        });

        while (true)
        {
            object step;
            try
            {
                if (!part.MoveNext())
                {
                    break;
                }

                step = part.Current;
            }
            catch (Exception e)
            {
                _checks.Fail("the part ran to its end", $"{e.GetType().Name}: {e.Message}");
                break;
            }

            yield return step;
        }

        yield return Idle();
    }

    /// <summary>Takes as the key does, then waits for every answer.</summary>
    private IEnumerator Grab(Target target)
    {
        var before = Snapshot();
        Grabber.Grab(_player, target, Time.time);
        yield return Idle();
        _checks.Note(string.Join(" | ", Grabber.LastLines));
        Conserved(before);
    }

    private Dictionary<string, int> Snapshot() => Items.ToDictionary(i => i, _site.Total);

    /// <summary>The inventory and the test chests hold together exactly what they held before.</summary>
    private void Conserved(Dictionary<string, int> before)
    {
        foreach (var item in Items)
        {
            int now = _site.Total(item);
            _checks.Check(now == before[item], $"no {Text.Shown(item)} was made or lost", $"{before[item]} before, {now} after");
        }
    }

    private bool Guarded(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            _checks.Fail(what, $"{e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    /// <summary>MultiUserChest holds the slot of a take that is never answered; the test lets it go.</summary>
    private void ReleaseMultiUserChestHold()
    {
        try
        {
            var assembly = Chainloader.PluginInfos[MucLink.Guid].Instance.GetType().Assembly;
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var block = assembly.GetType("MultiUserChest.InventoryBlock").GetMethod("Get", any).Invoke(null, [_site.Bag]);
            block.GetType().GetMethod("ReleaseBlockedSlots").Invoke(block, null);
            foreach (string table in new[] { "PackageChanges", "ToRemovePackages" })
            {
                object weak = assembly.GetType("MultiUserChest.InventoryPreview").GetField(table, any).GetValue(null);
                object[] args = [_site.Bag, null];
                if (weak.GetType().GetMethod("TryGetValue").Invoke(weak, args) is true && args[1] is IList list)
                {
                    list.Clear();
                }
            }
        }
        catch (Exception e)
        {
            _checks.Fail("let MultiUserChest's held slot go", $"{e.GetType().Name}: {e.Message}");
        }
    }

    private static IEnumerator Idle(float seconds = 15f) => Until(() => !Grabber.Busy, seconds);

    private static IEnumerator Until(Func<bool> done, float seconds)
    {
        float end = Time.time + seconds;
        while (!done() && Time.time < end)
        {
            yield return null;
        }
    }

    private static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }
}
