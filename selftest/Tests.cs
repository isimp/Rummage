using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

        // The test expects the settings as they ship; the player's own are put back at the end.
        var restoreSettings = Settings.UseDefaults();
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
            yield return Part("a chest under someone else's ward", Warded());
            yield return Part("a full inventory with one part stack", FullInventory());
            yield return Part("a recipe in the crafting tab, pressed twice", Recipe());
            yield return Part("filling a stack", TopUp());
            yield return Part("the search box", SearchBox());
            yield return Part("new items", NewToYou());
            yield return Part("the settings", SettingsChange());
            yield return Part("a chest held by a game that never answers", NeverAnswers());
            yield return Part("a chest held by another player's game", AnotherPlayer());
            yield return Part("how long things take", Timings());
        }

        _checks.Start("putting everything back");
        Guarded("putting everything back", _site.TearDown);
        Guarded("putting the player's settings back", restoreSettings);
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

        Plugin.Instance.BuildLine.Refresh();
        yield return Frames(5);
        var shown = Hud.instance && Hud.instance.m_buildHud ? Hud.instance.m_buildHud.transform.Find("Rummage/Text")?.GetComponent<TMPro.TMP_Text>() : null;
        string table = shown ? shown.text : "";
        string wood = table.Split('\n').FirstOrDefault(row => row.StartsWith("Wood<pos=", StringComparison.Ordinal)) ?? "";
        _checks.Check(shown && shown.gameObject.activeInHierarchy && table.StartsWith($"<color={Readout.Header}>For 3", StringComparison.Ordinal) && table.Contains(" takes</color>"),
            "the table shows beside the game's build panel, for 3", shown ? table : "not there");
        _checks.Check(wood.Contains($">{cost * 2} / {cost * 3}</color>") && wood.EndsWith($">{cost}</color>", StringComparison.Ordinal),
            "its wood row shows what you carry against 3 and what the next press takes", wood.Length > 0 ? wood : table);

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

        var palette = Plugin.Instance.Palette;
        palette.Open(_player, null);
        palette.Text = "wood";
        var wood = palette.Results.FirstOrDefault(e => e.Key == Site.Wood);
        _checks.Check(wood != null && wood.InChests == 0 && wood.Held > 0, "wood that cannot be taken is still found by the search", wood == null ? "not listed" : $"{wood.InChests} to take, {wood.Held} held");
        palette.Close();
    }

    private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownMaterials = AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownMaterial");

    /// <summary>
    /// /rummage new and the search box's New items row. Stone and wood arrows are made new to the
    /// character for the part by taking them off the list of what it has carried, and are on it
    /// again afterwards; a character that has not carried both yet is left alone, as the part would
    /// teach it them for real.
    /// </summary>
    private IEnumerator NewToYou()
    {
        var known = KnownMaterials(_player);
        string[] madeNew = [Site.Stone, Site.Arrows];
        if (known == null || !Items.All(known.Contains))
        {
            _checks.Skip("this character has not carried wood, stone, resin and wood arrows yet");
            yield break;
        }

        var terminal = (Terminal)global::Console.instance ?? Chat.instance;
        void RunNew() => terminal.TryRunCommand("rummage new", silentFail: false, skipAllowedCheck: true);

        RunNew();
        _checks.Check(Grabber.LastLines.Count == 1 && Grabber.LastLines[0] == Core.NewItems.NothingNew, "with nothing new nearby, /rummage new says so", string.Join(" | ", Grabber.LastLines));

        try
        {
            known.ExceptWith(madeNew);
            var palette = Plugin.Instance.Palette;
            palette.Open(_player, null);
            var first = palette.Results.Count > 0 ? palette.Results[0] : null;
            _checks.Check(first?.Kind == EntryKind.New, "the search box lists New items first", first?.Shown ?? "nothing");
            string parts = palette.ChosenParts;
            _checks.Check(parts.Contains(Text.Shown(Site.Stone)) && parts.Contains(Text.Shown(Site.Arrows)) && !parts.Contains(Text.Shown(Site.Wood) + ","),
                "the row names stone and wood arrows, not wood", parts);
            palette.Close();

            var before = Snapshot();
            RunNew();
            yield return Idle();
            _checks.Note(string.Join(" | ", Grabber.LastLines));
            Conserved(before);
            _checks.Check(Site.Count(_site.Bag, Site.Stone) == 1 && Site.Count(_site.Bag, Site.Arrows) == 1, "/rummage new takes one stone and one wood arrow",
                $"{Site.Count(_site.Bag, Site.Stone)} stone, {Site.Count(_site.Bag, Site.Arrows)} arrows");
            _checks.Check(Site.Count(_site.Bag, Site.Wood) == 0 && Site.Count(_site.Bag, Site.Resin) == 0, "nothing already known is taken");
            _checks.Check(madeNew.All(known.Contains), "carrying them taught the game both");
            _checks.Check(Grabber.LastLines.Count > 0 && Grabber.LastLines[0].StartsWith(Core.NewItems.Label + ": took", StringComparison.Ordinal), "the report says what was taken", string.Join(" | ", Grabber.LastLines));

            // One stone left in the chest: Leave One Item keeps it, as for any other grab.
            _site.ClearBag();
            _site.Refill();
            _site.Main.GetInventory().RemoveItem(Site.Stone, 19);
            known.Remove(Site.Stone);
            palette.Open(_player, null);
            before = Snapshot();
            palette.PressEnter(_player);
            yield return Idle();
            // The keys stay held for two frames after the box closes; the next part presses the key.
            yield return Frames(4);
            Conserved(before);
            string lines = string.Join(" | ", Grabber.LastLines);
            if (CraftyLink.LeaveOne)
            {
                _checks.Check(Site.Count(_site.Bag, Site.Stone) == 0 && Site.Count(_site.Main.GetInventory(), Site.Stone) == 1, "Enter on the row leaves the last stone with Leave One Item on", lines);
                _checks.Check(lines.Contains("kept back by Leave One Item"), "the report says it was kept back", lines);
            }
            else
            {
                _checks.Check(Site.Count(_site.Bag, Site.Stone) == 1, "Enter on the row takes the last stone with Leave One Item off", lines);
            }
        }
        finally
        {
            known.UnionWith(madeNew);
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

    /// <summary>Opens the inventory with the hammer's recipe selected in the crafting tab; null when it is not there.</summary>
    private Recipe SelectHammerRecipe()
    {
        var gui = InventoryGui.instance;
        AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel").Invoke(gui, [false]);
        var recipes = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes").GetValue(gui) as IList;
        for (int i = 0; recipes != null && i < recipes.Count; i++)
        {
            var pair = recipes[i];
            var candidate = pair.GetType().GetProperty("Recipe").GetValue(pair) as Recipe;
            if (candidate && candidate.m_item.m_itemData.m_shared.m_name == "$item_hammer" && pair.GetType().GetProperty("ItemData").GetValue(pair) == null)
            {
                AccessTools.Method(typeof(InventoryGui), "SetRecipe").Invoke(gui, [i, false]);
                return candidate;
            }
        }

        return null;
    }

    private IEnumerator Recipe()
    {
        var gui = InventoryGui.instance;
        gui.Show(null);
        yield return Frames(10);
        var recipe = SelectHammerRecipe();
        if (!recipe)
        {
            _checks.Skip("the hammer's recipe is not in the crafting tab here");
            gui.Hide();
            yield break;
        }

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
        var marked = Catalog.Build(_player, Scene.Read(_player)).Entries.Select(e => e.Shown)
            .Where(name => System.Text.RegularExpressions.Regex.IsMatch(name, @"<[A-Za-z/#]")).Take(5).ToList();
        _checks.Check(marked.Count == 0, "no name in the search box carries markup such as a colour", string.Join(", ", marked));
        _checks.Check(Palette.IsOpen && TextInput.IsVisible(), "while open, the game treats it as a text box");
        palette.Text = "40 wood";
        _checks.Check(palette.Results.Count > 0 && palette.Results[0].Key == Site.Wood, "\"40 wood\" puts wood first", palette.Results.Count > 0 ? palette.Results[0].Shown : "nothing");
        _checks.Check(palette.ChosenParts.Contains("carried + ") && palette.ChosenParts.Contains(" in chests"), "the chosen item shows what is carried and in the chests", palette.ChosenParts);
        palette.Text = "workbench";
        string parts = palette.Results.Count > 0 && palette.Results[0].Kind == EntryKind.Piece ? palette.ChosenParts : "(no workbench listed)";
        _checks.Check(parts.Contains("Wood ") && parts.Contains(" / "), "a chosen piece shows its parts carried and in the chests against the count", parts);
        palette.Text = "wood";
        int carried = Site.Count(_site.Bag, Site.Wood);
        palette.PressEnter(_player);
        string found = string.Join(" | ", Finder.LastLines);
        _checks.Check(!Palette.IsOpen, "Enter without a number closes the box to show where it is");
        _checks.Check(Site.Count(_site.Bag, Site.Wood) == carried, "finding takes nothing");
        _checks.Check(found.Contains("Wood: 110 in 3 chests"), "it finds the wood in the three test chests that may be used", found);
        _checks.Check(!Finder.LastLit.Contains(_site.Foreign), "someone else's private chest is not shown");
        _checks.Check(Finder.LastLit.Count == 3, "the three chests light up", Finder.LastLit.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        palette.Open(_player, null);
        var numbered = Catalog.Build(_player, Scene.Read(_player)).Entries
            .FirstOrDefault(e => e.Shown.Split(' ').Length > 1 && e.Shown.Split(' ').Last().All(char.IsDigit));
        if (numbered != null)
        {
            palette.Text = numbered.Shown;
            _checks.Check(palette.TypedCount == null && palette.Results.Count > 0 && palette.Results[0].Shown == numbered.Shown,
                $"\"{numbered.Shown}\" is read as the name, its number no count", palette.Results.Count > 0 ? palette.Results[0].Shown : "nothing");
            palette.Text = $"2 {numbered.Shown}";
            _checks.Check(palette.TypedCount == 2, $"\"2 {numbered.Shown}\" is two of it", palette.TypedCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no count");
        }
        else
        {
            _checks.Note("no known piece or recipe here has a name ending in a number");
        }

        // Typed in full but in upper case, so Wood itself ranks first whatever else the chests hold.
        string wood = Text.Shown(Site.Wood);
        palette.Text = $"40 {wood.ToUpperInvariant()}";
        palette.PressTab();
        _checks.Check(palette.Text == $"40 {wood}", $"Tab completes \"40 {wood.ToUpperInvariant()}\" to \"40 {wood}\"", palette.Text);
        palette.Text = $"{wood.ToUpperInvariant()} 40";
        palette.PressTab();
        _checks.Check(palette.Text == $"40 {wood}", "Tab puts a count typed after the name in front", palette.Text);
        palette.Text = "40 wo";
        palette.PressTab();
        _checks.Check(palette.Text == $"40 {wood}", $"Tab completes 40 wo to the shortest name it starts, 40 {wood}", palette.Text);
        int before = Site.Count(_site.Bag, Site.Wood);
        palette.PressEnter(_player);
        _checks.Check(!Palette.IsOpen && TextInput.IsVisible(), "Enter closes it and still holds the keys while Enter is down");
        yield return Frames(4);
        _checks.Check(!TextInput.IsVisible(), "a few frames later the keys are the game's again");
        yield return Idle();
        _checks.Check(Site.Count(_site.Bag, Site.Wood) - before == 40, "40 wood arrived", (Site.Count(_site.Bag, Site.Wood) - before).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// What the settings turn off or change, each through the same entry points as the key and the
    /// search box, and each set back to its default afterwards.
    /// </summary>
    private IEnumerator SettingsChange()
    {
        string KindsListed() => string.Join(", ", Catalog.Build(_player, Scene.Read(_player)).Entries.Select(e => e.Kind).Distinct().OrderBy(k => k));
        foreach (var (entry, kind) in new[] { (Settings.ShowItems, EntryKind.Item), (Settings.ShowPieces, EntryKind.Piece), (Settings.ShowRecipes, EntryKind.Recipe) })
        {
            entry.Value = false;
            var kinds = Catalog.Build(_player, Scene.Read(_player)).Entries.Select(e => e.Kind).ToHashSet();
            _checks.Check(!kinds.Contains(kind) && kinds.Count == 2, $"with {entry.Definition.Key} off the search box lists no {kind} entries, and the rest still", KindsListed());
            entry.Value = true;
        }

        // Nothing in front of the player here: no inventory open, nothing being placed.
        _checks.Check(Plugin.Instance.Press(_player, search: false) == Pressed.Opened && Palette.IsOpen, "the key with nothing in front opens the search box");
        Plugin.Instance.Palette.Close();
        // The keys stay held for two frames after the box closes, and a press is refused meanwhile.
        yield return Frames(4);
        Settings.KeyAloneOpensSearch.Value = false;
        _checks.Check(Plugin.Instance.Press(_player, search: false) == Pressed.Nothing && !Palette.IsOpen, "with KeyAloneOpensSearch off it does nothing there");
        _checks.Check(Plugin.Instance.Press(_player, search: true) == Pressed.Opened && Palette.IsOpen, "the search modifier still opens it");
        Plugin.Instance.Palette.Close();
        Settings.KeyAloneOpensSearch.Value = true;
        yield return Frames(4);

        var piece = ZNetScene.instance.GetPrefab("piece_workbench")?.GetComponent<Piece>();
        var hammer = Site.Make("Hammer", 1);
        _site.Bag.AddItem(hammer);
        _player.EquipItem(hammer, triggerEquipEffects: false);
        yield return Until(() => _player.InPlaceMode(), 3f);
        if (_player.InPlaceMode() && piece && _player.SetSelectedPiece(piece))
        {
            var box = Hud.instance && Hud.instance.m_buildHud ? Hud.instance.m_buildHud.transform.Find("Rummage") : null;
            Settings.TakeForPieces.Value = false;
            Plugin.Instance.BuildLine.Refresh();
            yield return Frames(5);
            box = box ? box : Hud.instance.m_buildHud.transform.Find("Rummage");
            _checks.Check(Context.Find(_player, hovered: false) == null, "with TakeForPieces off the key is not for the piece being placed");
            _checks.Check(!box || !box.gameObject.activeSelf, "and the build box does not show");
            Settings.TakeForPieces.Value = true;

            Settings.BuildBox.Value = BuildBox.UntilCarried;
            Plugin.Instance.BuildLine.Refresh();
            yield return Frames(5);
            box = Hud.instance.m_buildHud.transform.Find("Rummage");
            _checks.Check(box && box.gameObject.activeSelf, "with BuildBox UntilCarried the box shows while the workbench cannot be built");
            int cost = piece.m_resources.Sum(r => r.m_amount);
            _site.Bag.AddItem(Site.Make("Wood", cost));
            Plugin.Instance.BuildLine.Refresh();
            yield return Frames(5);
            _checks.Check(box && !box.gameObject.activeSelf, "and hides once the inventory holds enough to build it", $"{Site.Count(_site.Bag, Site.Wood)} wood carried");
            Settings.BuildBox.Value = BuildBox.Always;
            Plugin.Instance.BuildLine.Refresh();
            yield return Frames(5);
            _checks.Check(box && box.gameObject.activeSelf, "with BuildBox Always it shows then too");
        }
        else
        {
            _checks.Skip("the hammer could not be taken up with the workbench selected");
        }

        _site.PutHandsBack();
        _site.ClearBag();

        var gui = InventoryGui.instance;
        gui.Show(null);
        yield return Frames(10);
        if (SelectHammerRecipe())
        {
            Settings.TakeForRecipes.Value = false;
            _checks.Check(Context.Find(_player, hovered: false) == null, "with TakeForRecipes off the key is not for the selected recipe");
            Settings.TakeForRecipes.Value = true;
            _checks.Check(Context.Find(_player, hovered: false)?.Kind == EntryKind.Recipe, "turned back on, it is");
        }
        else
        {
            _checks.Skip("the hammer's recipe is not in the crafting tab here");
        }

        gui.Hide();
        yield return Frames(4);

        var (x, y) = _site.FreeSlot();
        _site.Bag.AddItem(Site.Make("ArrowWood", 23), new Vector2i(x, y));
        Settings.TopUpAmount.Value = 20;
        var topUp = Target.ForTopUp(Scene.StackOf(_site.Bag.GetItemAt(x, y)), Settings.TopUpAmount.Value);
        _checks.Check(!topUp.FillsStack && topUp.Count == 20, "with TopUpAmount 20 a press over a stack is for 20", topUp.Label);
        yield return Grab(topUp);
        _checks.Check(_site.Bag.GetItemAt(x, y)?.m_stack == 43, "and 20 arrive in that stack", _site.Bag.GetItemAt(x, y)?.m_stack.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Settings.TopUpAmount.Value = 0;

        Settings.Messages.Value = Messages.Short;
        yield return Grab(Target.ForItem(Site.Wood, Text.Shown(Site.Wood), 1000));
        string lines = string.Join(" | ", Grabber.LastLines);
        _checks.Check(Grabber.LastLines.Count == 2 && Grabber.LastLines[1].StartsWith("Short: ", StringComparison.Ordinal) && !lines.Contains(" short: "),
            "with Messages Short a grab names what is short without the reasons", lines);
        Settings.Messages.Value = Messages.Off;
        // The grab above emptied the test chests of wood.
        _site.ClearBag();
        _site.Refill();
        yield return Grab(Target.ForItem(Site.Wood, Text.Shown(Site.Wood), 5));
        _checks.Check(Grabber.LastLines.Count == 0 && Site.Count(_site.Bag, Site.Wood) == 5, "with Messages Off it still takes and says nothing", $"{Site.Count(_site.Bag, Site.Wood)} wood; {string.Join(" | ", Grabber.LastLines)}");
        Settings.Messages.Value = Messages.Full;
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

    /// <summary>
    /// A chest AzuCraftyBoxes listed earlier, now under a ward owned by nobody: Rummage must not take
    /// from it nor show it, as the player could not open it by hand. Skipped when the game lets the
    /// player into every ward, as a server admin bypass does.
    /// </summary>
    private IEnumerator Warded()
    {
        var ward = _site.PlaceWard(_site.Main);
        yield return Frames(10);
        try
        {
            if (PrivateArea.CheckAccess(_site.Main.transform.position, 0f, false, false))
            {
                _checks.Skip("the game lets you open chests under any ward here, as an admin bypass (such as in god mode) does");
                yield break;
            }

            CraftyLink.OnlyChests = chest => chest == _site.Main;
            _checks.Check(CraftyLink.NearbyChests(_player).Contains(_site.Main), "AzuCraftyBoxes still lists the chest, from before the ward");
            int chestWood = Site.Count(_site.Main.GetInventory(), Site.Wood);
            yield return Grab(Target.ForItem(Site.Wood, "Wood", 5));
            _checks.Check(Site.Count(_site.Bag, Site.Wood) == 0, "nothing is taken from it");
            _checks.Check(Site.Count(_site.Main.GetInventory(), Site.Wood) == chestWood, "the chest keeps its wood");
            _checks.Check(Grabber.LastLines.Any(l => l.Contains("in chests you may not take from")), "the report names it as a chest you may not take from", string.Join(" | ", Grabber.LastLines));
            Finder.Find(_player, Target.ForItem(Site.Wood, "Wood", 1));
            _checks.Check(!Finder.LastLit.Contains(_site.Main), "finding does not show it");
        }
        finally
        {
            Site.TakeDownWard(ward);
        }
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

    /// <summary>
    /// Times each step on the chests really in reach, read only, and one grab on the test chests:
    /// milliseconds each, roughly how much memory each made for the collector, and the sizes
    /// involved. Notes only; what counts as slow depends on the machine.
    /// </summary>
    private IEnumerator Timings()
    {
        CraftyLink.OnlyChests = null;
        var nearby = CraftyLink.NearbyChests(_player);
        _checks.Note($"{nearby.Count} chests in reach holding {nearby.Sum(c => c.GetInventory()?.NrOfItems() ?? 0)} stacks, Leave One Item {(CraftyLink.LeaveOne ? "on" : "off")}");
        var collider = _site.Main.GetComponentInChildren<Collider>();
        _checks.Note($"a test chest sits on the layer {(collider ? LayerMask.LayerToName(collider.gameObject.layer) : "(none)")}");

        Measure("reading the chests in reach", 20, () => Scene.Read(_player));
        Catalog.Forget();
        Measure("building the search list, first time", 1, () => Catalog.Build(_player, Scene.Read(_player)), warm: false);
        Measure("building the search list, costs kept", 10, () => Catalog.Build(_player, Scene.Read(_player)));
        var catalog = Catalog.Build(_player, Scene.Read(_player));
        _checks.Note($"the search list holds {catalog.Entries.Count} entries ({catalog.Entries.Count(e => e.Kind == EntryKind.Item)} items, {catalog.Entries.Count(e => e.Kind == EntryKind.Piece)} pieces, {catalog.Entries.Count(e => e.Kind == EntryKind.Recipe)} recipes)");
        foreach (string text in new[] { "w", "wo", "wood", "bronze nails", "wkb" })
        {
            Measure($"ranking \"{text}\"", 50, () => Search.Rank(text, catalog.Entries));
        }

        Measure("the shortfall scan", 10, () => Scene.Read(_player).AddBarred(_player));
        var workbench = ZNetScene.instance.GetPrefab("piece_workbench")?.GetComponent<Piece>();
        if (workbench)
        {
            var scene = Scene.Read(_player);
            Measure("planning a press for the workbench", 50, () => Planner.Make(Target.ForPiece(workbench).Wants(), scene.Sources, scene.Bag, CraftyLink.LeaveOne));
        }

        var (worked, skipped) = Plugin.Instance.BuildLine.Counts;
        _checks.Note($"the build panel lines were worked out {worked} times and left alone {skipped} times as nothing had changed");

        CraftyLink.OnlyChests = chest => _site.Sources.Contains(chest);
        var before = Snapshot();
        var watch = Stopwatch.StartNew();
        Grabber.Grab(_player, Target.ForItem(Site.Wood, "Wood", 5), Time.time);
        watch.Stop();
        _checks.Note($"a grab of 5 wood from the test chests: {watch.Elapsed.TotalMilliseconds:F2} ms");
        yield return Idle();
        Conserved(before);
    }

    /// <summary>Runs a step a number of times and notes its time and memory per run; a first untimed run warms it unless told not to.</summary>
    private void Measure(string what, int times, Action step, bool warm = true)
    {
        try
        {
            if (warm)
            {
                step();
            }

            long memory = GC.GetTotalMemory(false);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < times; i++)
            {
                step();
            }

            watch.Stop();
            long made = Math.Max(0, GC.GetTotalMemory(false) - memory);
            _checks.Note($"{what}: {watch.Elapsed.TotalMilliseconds / times:F2} ms each, about {made / 1024.0 / times:F1} KB made each");
        }
        catch (Exception e)
        {
            _checks.Fail($"time {what}", $"{e.GetType().Name}: {e.Message}");
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
