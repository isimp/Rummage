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
            yield return Until(AllListed, 6f);
            _checks.Check(AllListed(), "AzuCraftyBoxes lists the test chests", "it did not within 6 seconds");
            if (!AllListed())
            {
                ExplainUnlisted();
                yield return Until(AllListed, 2f);
                built = AllListed();
                _checks.Check(built, "the test chests are listed once handed to AzuCraftyBoxes' own API");
            }
        }

        if (built)
        {
            yield return Part("a build piece placed with the hammer, pressed twice", HammerPiece());
            yield return Part("Leave One Item", LeaveOne());
            yield return Part("chest rules", ChestRules());
            yield return Part("chests that may not be used", Barred());
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
            string expect = $"{target.Label}: Wood {cost * press}/{cost * press} (+{cost})";
            _checks.Check(Grabber.LastLines.Count > 0 && Grabber.LastLines[0].StartsWith(expect, StringComparison.Ordinal), $"press {press}: the summary reads \"{expect}\"", string.Join(" | ", Grabber.LastLines));
        }

        var next = Context.PieceTarget(_player);
        var scene = Scene.Read(_player);
        string line = Readout.Line(next.Label, Planner.Make(next.Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne), "J", Text.Shown);
        _checks.Check(line.Contains($"in your bag: Wood {cost * 2}/{cost * 3}") && line.Contains($"J brings {cost} Wood"), "while building, the line shows the bag and what the next press brings", line);

        yield return CountingPastAMissingPart();
        _site.PutHandsBack();
    }

    /// <summary>
    /// A known piece that needs wood and a part the test chests do not hold: each press still counts
    /// up, the wood piles up, and the missing part is named short.
    /// </summary>
    private IEnumerator CountingPastAMissingPart()
    {
        // What the test chests give; resin only sits in chests whose rules or owner keep it in.
        var held = new HashSet<string>(new[] { Site.Wood, Site.Stone, Site.Arrows });
        var piece = _player.GetBuildPieces()?.FirstOrDefault(p => p && p.m_resources.Any(r => r.m_resItem && r.m_resItem.m_itemData.m_shared.m_name == Site.Wood)
            && p.m_resources.Any(r => r.m_resItem && r.m_amount > 0 && !held.Contains(r.m_resItem.m_itemData.m_shared.m_name)));
        if (!piece || !_player.SetSelectedPiece(piece))
        {
            _checks.Skip("no known piece needs wood and something the test chests lack");
            yield break;
        }

        _checks.Note($"counting up with {Text.Shown(piece.m_name)}");
        var given = piece.m_resources.Where(r => r.m_amount > 0 && held.Contains(r.m_resItem.m_itemData.m_shared.m_name))
            .GroupBy(r => r.m_resItem.m_itemData.m_shared.m_name).ToDictionary(g => g.Key, g => g.Sum(r => r.m_amount));
        string missing = piece.m_resources.First(r => r.m_amount > 0 && !held.Contains(r.m_resItem.m_itemData.m_shared.m_name)).m_resItem.m_itemData.m_shared.m_name;
        int before = -1;
        for (int press = 1; press <= 2; press++)
        {
            var target = Context.Find(_player, hovered: false);
            int expected = given.Min(p => Site.Count(_site.Bag, p.Key) / p.Value) + 1;
            _checks.Check(target?.Count == expected, $"press {press}: it counts up to {expected} though {Text.Shown(missing)} is missing", target?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _checks.Check(before < 0 || target?.Count == before + 1, $"press {press}: one more than the press before");
            before = target?.Count ?? -1;
            if (target == null)
            {
                yield break;
            }

            yield return Grab(target);
            _checks.Check(Grabber.LastLines.Any(l => l.Contains($"{Text.Shown(missing)} short")), $"press {press}: {Text.Shown(missing)} is named short", string.Join(" | ", Grabber.LastLines));
        }
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
            _checks.Check(Grabber.LastLines.Any(l => l.Contains("kept back by Leave One Item")), "the report says what was kept back", string.Join(" | ", Grabber.LastLines));
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
            // The parts the crafting tab asks for away from any station: none an upgrade station adds.
            foreach (var requirement in recipe.m_resources.Where(r => r.GetAmount(1) > 0 && !r.m_upgraderResource))
            {
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int want = requirement.GetAmount(1) * step * press;
                if (_site.Total(name) < want)
                {
                    _checks.Note($"press {press}: the test chests hold too little {Text.Shown(name)} to check it");
                    continue;
                }

                _checks.Check(Site.Count(_site.Bag, name) >= want, $"press {press}: the inventory holds {want} {Text.Shown(name)}", Site.Count(_site.Bag, name).ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        _checks.Check(palette.Results.Count > 0, "the search box lists what the test chests hold");
        _checks.Check(Palette.IsOpen && TextInput.IsVisible(), "while open, the game treats it as a text box");
        palette.Text = "40 wood";
        _checks.Check(palette.Results.Count > 0 && palette.Results[0].Key == Site.Wood, "\"40 wood\" puts wood first", palette.Results.Count > 0 ? palette.Results[0].Shown : "nothing");
        _checks.Check(palette.ChosenParts.Contains("carried + ") && palette.ChosenParts.Contains(" in chests"), "the chosen item shows what is carried and in the chests", palette.ChosenParts);
        palette.Text = "workbench";
        string parts = palette.Results.Count > 0 && palette.Results[0].Kind == EntryKind.Piece ? palette.ChosenParts : "(no workbench listed)";
        _checks.Check(parts.Contains("Wood ") && parts.Contains(" / "), "a chosen piece shows its parts carried and in the chests against the count", parts);
        palette.Text = "40 wood";
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
        view.GetZDO().SetOwner(Site.Nobody);
        _checks.Check(Scene.ReachOf(_site.Main, view) == Reach.Routed, "a chest another game holds goes through MultiUserChest");

        var before = Snapshot();
        Grabber.Grab(_player, Target.ForItem(Site.Wood, "Wood", 5), Time.time);
        _checks.Check(Site.Count(_site.Main.GetInventory(), Site.Wood) == 50, "the chest is not changed in this game");
        _checks.Check(Site.Count(_site.Bag, Site.Wood) == 0, "nothing arrives without an answer");
        _checks.Check(Grabber.Busy, "the next grab waits for the answer");
        _checks.Check(Grabber.LastLines.Count > 0 && Grabber.LastLines[0].Contains("asked another player's chest for 5 "), "the report says it was asked for, not taken", string.Join(" | ", Grabber.LastLines));
        yield return Idle(15f);
        _checks.Check(!Grabber.Busy && Grabber.LastUnanswered > 0, "the wait ends after its timeout");
        _checks.Check(Grabber.LastArrivals.Any(l => l.Contains("did not come: the other chest's game did not answer")), "the player is told it did not come", string.Join(" | ", Grabber.LastArrivals));
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
        _checks.Check(Grabber.LastArrivals.Any(l => l.EndsWith(" arrived", StringComparison.Ordinal)), "the player is told it arrived", string.Join(" | ", Grabber.LastArrivals));
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

    /// <summary>
    /// Someone else's private chest is named without what it holds; a chest someone else has open
    /// is named with it. For the second, the chest is handed to an owner who does not exist and
    /// marked open, which the game undoes within two seconds, so it is tried a few times.
    /// </summary>
    private IEnumerator Barred()
    {
        yield return Grab(Target.ForItem(Site.Resin, "Resin", 5));
        string lines = string.Join(" | ", Grabber.LastLines);
        _checks.Check(lines.Contains("some in someone's private chest"), "someone else's private chest is named", lines);
        _checks.Check(!System.Text.RegularExpressions.Regex.IsMatch(lines, @"\d+ in someone's private chest"), "without telling what it holds", lines);

        CraftyLink.OnlyChests = chest => chest == _site.Second;
        var view = Scene.View(_site.Second);
        var zdo = view.GetZDO();
        bool tried = false;
        for (int attempt = 0; attempt < 5 && !tried; attempt++)
        {
            yield return Until(view.IsOwner, 3f);
            zdo.Set(ZDOVars.s_inUse, 1);
            zdo.SetOwner(Site.Nobody);
            yield return Until(() => false, 0.35f);
            if (view.IsOwner())
            {
                continue;
            }

            tried = true;
            yield return Grab(Target.ForItem(Site.Wood, "Wood", 5));
            lines = string.Join(" | ", Grabber.LastLines);
            _checks.Check(lines.Contains("50 in a chest someone has open"), "a chest someone has open is named with what it holds", lines);
        }

        if (!tried)
        {
            _checks.Skip("the game took the chest back every time before the grab");
        }

        yield return Until(view.IsOwner, 3f);
        if (!view.IsOwner())
        {
            view.ClaimOwnership();
        }

        zdo.Set(ZDOVars.s_inUse, 0);
    }

    private bool AllListed() => _site.Listed.All(c => CraftyLink.NearbyChests(_player).Contains(c));

    /// <summary>
    /// For each test chest AzuCraftyBoxes does not list: what it checks before listing a chest,
    /// then the chest handed to its own API so the remaining parts can run.
    /// </summary>
    private void ExplainUnlisted()
    {
        try
        {
            var crafty = Chainloader.PluginInfos[CraftyLink.Guid].Instance.GetType().Assembly;
            var registry = crafty.GetType("AzuCraftyBoxes.Util.Functions.Boxes").GetField("Containers", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as HashSet<Container>;
            _player.m_customData.TryGetValue("ACB_PreventPulling", out string pull);
            _checks.Note($"AzuCraftyBoxes pull mode is {pull ?? "unset (all)"} (0 off, 1 all, 2 mine only); it registers no chests while it is off");
            var listed = CraftyLink.NearbyChests(_player);
            foreach (var chest in _site.Listed.Where(c => !listed.Contains(c)))
            {
                var zdo = Scene.View(chest).GetZDO();
                _checks.Note($"{chest.name} at {Site.Where(chest)}: in its registry {registry?.Contains(chest).ToString() ?? "unknown"}, creator {zdo.GetLong(ZDOVars.s_creator)} / piece {chest.GetComponentInParent<Piece>()?.GetCreator()}, you {_player.GetPlayerID()}, privacy {chest.m_privacy}, ward lets you {PrivateArea.CheckAccess(chest.transform.position, 0f, false, true)}, in use {zdo.GetInt(ZDOVars.s_inUse)}, distance {Vector3.Distance(chest.transform.position, _player.transform.position):F1} m");
                crafty.GetType("AzuCraftyBoxes.API").GetMethod("AddContainer").Invoke(null, [chest]);
            }
        }
        catch (Exception e)
        {
            _checks.Fail("explain why the test chests are not listed", $"{e.GetType().Name}: {e.Message}");
        }
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
