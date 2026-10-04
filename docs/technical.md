# Technical notes

How Rummage works, which other mods it relies on and how, and how it is built and tested. Rummage runs only in the game client (`BepInProcess("valheim.exe")`); a dedicated server never loads it.

## Dependencies

| Mod | How Rummage needs it |
|---|---|
| AzuCraftyBoxes 1.8.27 or later | Required. A hard `BepInDependency` with that minimum version and the package manifest's dependency, so BepInEx does not load Rummage without it and mod managers install it alongside. It decides which chests Rummage may use and what may leave them. |
| MultiUserChest | Optional. With it, chests another player's game holds can be used; without it, they are left alone. MultiUserChest itself must be on the server and every client. |
| ExtraSlots | Optional. With it, its equipment, quick, food and ammo slots are never filled, except the stack a top-up fills. |
| AzuAntiArthriticCrafting | Optional. With it, a recipe is taken for the craft amount set in its crafting tab. |

Every one of them is reached by type and member name at startup, never as a build reference, so Rummage builds without them. A member that cannot be found is logged once and its part is switched off. The startup line in the log says what was found.

## Layout

`src/Core` decides everything that needs no game: the plan of a grab (`Planner`), what the chests can give (`Stock`), how many a press is for (`Repeat`), costs (`Cost`), the search (`Search`, with `Query` reading the text), every line the player reads (`Report`, `Readout`, `Locate`) and the wait for other players' answers (`Waiting`). It has no Unity or BepInEx reference, so `tests/Rummage.Tests` builds it on its own. `src/Game` reads the game and the other mods into the core's terms, carries out the result and draws what the player sees. `src/Plugin.cs` reads the keys and `src/Settings.cs` holds the settings. `selftest/` is the in-game self-test, a plugin of its own.

## The key

With the inventory open, the key is for a stack of the player's under the cursor (found as the inventory grid finds it, `InventoryGrid.GetHoveredElement` and `GetElementPos`), else the recipe selected in the crafting tab (`InventoryGui.m_selectedRecipe`). Otherwise it is for the build piece under the cursor in the build menu (`Hud.m_hoveredPiece`), else the piece being placed (`Player.GetSelectedPiece`), which is the piece the game's build panel shows. Each of these can be turned off (`TopUpStacks`, `TakeForRecipes`, `TakeForPieces`), and is then passed over. With none of them, the key opens the search box, unless `KeyAloneOpensSearch` is off; with the search modifier (`SearchModifier`, Shift by default) or a search key of its own (`SearchKey`), it opens the search box on the recipe or piece, its count filled in. All of this is `Plugin.Press`, which the self-test presses too.

A piece or recipe is a total to carry: what the inventory already holds counts, and only items at or above the world level count, as building and crafting only take those. A recipe follows the crafting tab's rules: a new item at quality 1, an upgrade at the next level, each requirement at `Requirement.GetAmount(quality)`, an upgrade station's own parts only at such a station. A new item is taken for AzuAntiArthriticCrafting's craft amount (`HaveRequirementItemsTranspiler.GetCurrentCraftAmount`), or for one craft without it. For a recipe that needs only one ingredient (`m_requireOnlyOneIngredient`), only ingredients the player knows are offered, as in the game; each is planned on its own, and the one carried in full already is chosen, else the first the chests make up in full, else the closest (`Choice.Best`).

Each press is for one more than what is carried already pays for (`Repeat.Next`): a piece counts what the inventory pays for in whole pieces and asks for one more, and a recipe steps by the craft amount. Only parts the chests can still give decide what is paid for, so a part they have run out of does not hold the count down; when no part can come from the chests, all parts decide and the count stays.

A stack under the cursor is filled to a full one, or by at most `TopUpAmount` when that is above zero (`Want.TopUp`): only items that stack with it (same item and world level, and quality for items with quality levels), and only into its own slot, whatever kind of slot that is.

## Where from

The chests are AzuCraftyBoxes' list (`API.GetNearbyContainers` at its own `Container Range`). That list already leaves out chests the player may not open, warded chests, chests open in another game, carts in use and chests its own toggles exclude; mods that seal chests from automation keep them out of it. Entries that are not vanilla chests (item drawers, the player's bags) are skipped. AzuCraftyBoxes checks access only when it first lists a chest and never takes a chest off its list, so before Rummage takes from, finds in or counts a chest, it asks again what the game asks when a chest is opened by hand (`Game/Access.cs`): the game's ward check, and so the rules of any mod that changes it, and the chest's privacy. A chest the player could not open now is only read, to say why something is missing. A chest's rules come from `API.CanItemBePulled` with the chest's and the item's prefab names. `Leave One Item` and `Container Range` are read from AzuCraftyBoxes' own config entries, so a server's synced values apply.

## The plan

`Core/Planner.cs` takes the nearest chest first. With `Leave One Item` on, each chest keeps one unit of each item it holds, counted by item rather than by stack, as AzuCraftyBoxes counts it (`Stock.Budget`, which the search list's counts use too). Each take is sized to land whole in one inventory slot: a part stack of exactly the same kind first, then an empty slot ordinary items may use. Takes from chests this game holds happen at once, so several may fill one slot; a take from another player's chest is answered later and its slot stays reserved until then (MultiUserChest's `InventoryBlock`), so that slot gets nothing more in the same grab, and a top-up waiting that way says to press again for the rest.

## The takes

Each take is `Inventory.MoveItemToThis(chest, item, amount, x, y)`, the call the inventory screen makes for a drag. It adds to the inventory and lowers the chest's stack in the same step, so an item is never in both places.

The game saves a chest only in the game that holds it (its ZDO owner); a change made anywhere else would be lost the next time the chest reloads, while the items would already be in the player's inventory. So:

| The chest is held by | Rummage |
|---|---|
| This game | Takes directly. |
| Another game, MultiUserChest loaded | Hands the take to MultiUserChest, which asks that game; that game removes the items and sends them back. |
| Another game, no MultiUserChest | Leaves the chest alone. |
| No game, or MultiUserChest ignores it | Leaves the chest alone. |

MultiUserChest takes over the same call through its `Inventory.AddItem` prefix, but only for an item it knows to be in that chest. Before each take Rummage checks that it does (`InventoryPatch.InventoryOfItem`), and after it that the chest is unchanged here and the target slot is held for the answer (`InventoryBlock.IsSlotBlocked`); a take that fails either check is not counted. The next grab waits until every slot of the last one is released, or 10 seconds. If the chest changed meanwhile, the holding game hands over what is now in that slot; if the player leaves before the answer, the answer is lost. Both are how MultiUserChest works for a drag too.

## What the player reads

After a press (`Core/Report.cs`): for a piece or recipe, each part as now carried over needed, with what the press added and what is on its way from another player's chest; for an item, what was taken and what was asked of other players' chests. Each shortfall gets a reason: kept back by `Leave One Item`, kept in by chest rules, in a chest someone has open, in someone's private chest (named without its count), in a chest the player may not take from, in a chest another game holds, no room, or not in chests nearby. For these reasons the chests in reach that AzuCraftyBoxes does not list are read too (`Scene.AddBarred`: its own registry, then one physics query over the building-piece and cart layers into a reused buffer), never to take from. Once every answer from other players' chests is in, `Report.Arrivals` says per item what arrived, what was turned down and what had no answer. How much is told follows `Messages`: everything (the default), what was taken and what is short without the reasons, or nothing after a grab or an answer; what finding turns up is always told. Lines go to the corner as the game's own messages, or, while the inventory is open and covers the corner, as one message in the middle of the screen (`Game/Notice.cs`). Chests taken from light up for two seconds.

While building, a small table in a box to the right of the game's build panel, as tall as the panel and at most 520 units wide (`Game/BuildReadout.cs`, text from `Readout.Panel`), shows a row per part: what the inventory holds against the next press, orange where the press would make it up and red where it cannot, and how many the press would take, with what would still be short. Its columns are placed with TextMeshPro's `pos` tag after the widest entry before them, measured in the text's own font. `BuildBox` set to `UntilCarried` hides it while the inventory holds enough to build one of the piece, and it never shows while `TakeForPieces` is off. The text is a duplicate of the panel's own description text object in a dark box, so it has the game's font. It is looked at twice a second and right after a press, and worked out again only when a fingerprint of what it depends on changes: the piece, every inventory stack, each chest in reach with its data revision and holder, and the settings.

## The search box

`Game/Palette.cs`, drawn with Unity's immediate-mode GUI in the game's own art (`Game/UiArt.cs`): sprites from the game's `UIAtlas` (`woodpanel_settings`, `text_field`, `button`, `button_highlight`, `button_small`) drawn as nine-part frames, a dark inner panel so text stands out on the wood, item, piece and recipe icons and the game's orange and beige. Where a sprite cannot be had, plain fills stand in. It is sized to the screen's height times `Scale` and shows `Rows` results at once. It draws through a component switched on only while it is open, and once early in a world to warm the fonts (the first text drawn in a session otherwise costs the better part of a second).

When it opens it reads the chests once (`Game/Catalog.cs`), listing what `ShowItems`, `ShowPieces` and `ShowRecipes` leave on: every item the chests within the find radius hold, with how many of those a grab could take, so an item that cannot be taken is still listed and can be found; every piece the build menus would show (enabled and in the player's known recipes); and every known recipe at quality 1. Pieces and recipes with their costs and icons are kept between openings and made again only when the known recipes or materials, the player, the world's item list or the language change.

`Core/Search.cs` reads the text against the names it may stand for (`Query.Parse`): when its words are the first words of a name, numbers included, it is that name; otherwise a whole number at either end is the count. It ranks the whole name first, then names starting with the text, then a word starting with it, then the text inside a word, then its letters in order; among equal matches the shorter name comes first, then what the chests hold most of. With nothing typed, it lists everything the chests hold by name. Tab (`Query.Complete`) puts the chosen name in, the count in front.

Enter without a count finds (`Game/Finder.cs`, lines from `Core/Locate.cs`): every chest AzuCraftyBoxes has registered within the find radius (`FindRadius`, 50 m by default; the ones the player may use and ones someone else has open; other players' private chests are never registered) is counted for the item, or for each part of a piece or recipe, and the chests holding any are lit for 6 seconds with the game's own highlight, given again every tenth of a second as it fades after a fifth (`Game/Highlighter.cs`). Enter with a count takes it; *Take* takes the slider's count.

While it is open, `TextInput.IsVisible` answers yes, which stops the player's movement, actions, chat and menu and frees the cursor; the inventory's update waits, as it reads Tab, Escape and the use key without that check; and the mouse wheel reads nothing outside the box. All three hold for two frames after it closes, and the game's chat and inventory keys are cleared then, so the Enter or Escape that closed it opens nothing. Each of these patches stands aside for the session if it ever fails.

## Self-test

`selftest/` is a plugin of its own, `Rummage.SelfTest`, reaching into Rummage through `InternalsVisibleTo`. It is built and deployed with a development build and built in CI so it keeps up, and it is never packaged: a release takes `Rummage.dll` alone. `/rummage selftest` in chat runs it where the player stands, and the results are `[selftest]` lines in `LogOutput.log` ending with a `DONE` summary.

It runs on the settings as they ship, without writing them to the config file, and puts the player's own back at the end. It sets the player's belongings (all but what is equipped) aside in vault chests it builds, builds test chests in front of the player (two to take from, a private one, and someone else's private one) and narrows Rummage's chests to them while it runs (`CraftyLink.OnlyChests`). Each part starts from refilled chests and an emptied inventory and goes through the key's own entry points (`Context.Find`, `Grabber.Grab`, the search box's text, Tab and Enter). After every grab it checks that the inventory and the test chests together hold exactly what they held before. Its parts cover a piece selected with the hammer pressed twice, counting past a missing part, the build panel lines, `Leave One Item`, chest rules, chests that may not be used, a listed chest put under a ward owned by nobody (skipped when the game lets the player into every ward, as an admin bypass does), a full inventory with one part stack, a recipe pressed twice, filling a stack, the search box (find, take, Tab, names ending in a number), the settings (what the search box lists, the key with nothing in front, the targets turned off, `BuildBox`, `TopUpAmount`, `Messages`), a chest handed to an owner that does not exist (MultiUserChest's request is never answered: the chest must not change here and the wait must end), a chest handed to a nearby player's game the way opening a chest hands it over (skipped when nobody else is within 30 m), and how long each step takes on the chests really in reach. Everything is put back at the end; what cannot be put back stays in its vault chest, left standing and reported with its place.

## Building

```
dotnet build                     # against the installed game, deploys to the Gale profile
dotnet build selftest            # Rummage and the self-test
dotnet build -p:LibsDir=lib      # against the stubs in lib/, as CI does
dotnet test tests/Rummage.Tests
```

`VALHEIM_DIR` or `-p:ValheimDir=` points at the game, `-p:ProfileDir=` at the BepInEx profile.
