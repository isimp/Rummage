# Technical notes

## Layout

`src/Core` decides a grab and has no Unity or BepInEx reference, so `tests/Rummage.Tests` builds it on its own. `src/Game` reads the game and the other mods into the core's terms and carries out the result. `src/Plugin.cs` holds the key.

## A grab

1. **What for.** In order: with the inventory open, a stack of the player's under the cursor (`InventoryGrid.GetHoveredElement`, `GetElementPos`), then the recipe selected in the crafting tab (`InventoryGui.m_selectedRecipe`), the build piece under the cursor in the build menu (`Hud.m_hoveredPiece`), the piece being placed (`Player.GetSelectedPiece`). Nothing of the sort opens the search box, and Shift opens it on the recipe or piece. A cost is a total to carry: what the inventory already holds counts, and only items at or above the world level count, as building and crafting only take those.

   Each press is for one more than what is carried already pays for (`Core/Repeat.cs`): a piece counts what the inventory pays for in whole pieces and asks for one more; a recipe steps by the craft amount, so with 5 set and enough carried for 7 crafts, the press is for 10. For a recipe that needs any one ingredient, the best covered ingredient counts.

   A stack is filled to a full one: only items that stack with it (same item and world level, and quality for items with quality levels), and only into its own slot, whatever kind of slot that is, so an ammo slot of ExtraSlots is filled too.

   A recipe follows the crafting tab's own rules: a new item at quality 1, an upgrade at the next level, each requirement at `Requirement.GetAmount(quality)`, an upgrade station's own parts only at such a station and never without one. A new item is taken for the craft amount AzuAntiArthriticCrafting is set to (`HaveRequirementItemsTranspiler.GetCurrentCraftAmount`), or for one craft without it. A recipe that needs only one ingredient (`m_requireOnlyOneIngredient`) offers only ingredients the player knows, as the game does; each is planned on its own, and the one carried in full already is chosen, else the first the chests make up in full, else the closest (`Core/Stock.cs`, `Choice.Best`).
2. **Where from.** AzuCraftyBoxes' `API.GetNearbyContainers` at its own `Container Range`. That list already leaves out chests the player may not open, warded chests, chests open in another game and chests its own toggles exclude; mods that seal chests from automation keep them out of it. Entries that are not vanilla chests (item drawers, the player's bags) are skipped.
3. **The plan** (`Core/Planner.cs`). Nearest chest first. A chest's rules (`API.CanItemBePulled` with the chest's and the item's prefab names) can keep an item in. With AzuCraftyBoxes' `Leave One Item` on, each chest keeps one unit of each item it holds, counted by item rather than by stack, as AzuCraftyBoxes counts it. Each take is sized to land whole in one inventory slot: a part stack of exactly the same kind first (same item and world level, and the same quality for items with quality levels), then an empty slot. ExtraSlots' own slots are never targets, except the stack a top-up fills. Takes from chests this game holds happen at once, so several may fill one slot; a slot that waits for another game's answer gets nothing more in the same grab, as MultiUserChest holds it until then. A top-up whose slot waits that way says to press again for the rest.
4. **The takes** (`Game/Grabber.cs`). Each one is `Inventory.MoveItemToThis(chest, item, amount, x, y)`, the call the inventory screen makes for a drag. It adds to the inventory and lowers the chest's stack in the same step, so an item is never in both places.
5. **The report** (`Core/Report.cs`): what was taken, and for each shortfall the units kept back by `Leave One Item`, kept in by chest rules, in chests out of reach, or that there was no room.

## The search box

`Game/Palette.cs`, drawn with Unity's immediate-mode GUI. When it opens it reads the chests once (`Game/Catalog.cs`): every item they can give by the same rules as a grab (`Core/Stock.cs`, which the planner uses too), every piece the build menu would show (enabled and in the player's known recipes), and every known recipe at quality 1. Pieces and recipes show how many of them what is carried and what the chests give would pay for.

`Core/Search.cs` parses the text (a whole number at either end is the count) and ranks: the whole name, then names starting with the text, then a word starting with it, then the text inside a word, then its letters in order. Equal matches put what the chests hold most of first. With nothing typed it lists the chests' items by name.

While it is open, `TextInput.IsVisible` answers yes, which stops the player's movement, actions, chat and menu and frees the cursor. The inventory's update waits, as it reads Tab, Escape and the use key without that check, and the mouse wheel reads nothing outside the box. Each of these patches stands aside for the session if it ever fails.

## Who may change a chest

The game saves a chest only in the game that holds it (its ZDO owner). A change made anywhere else is lost the next time the chest reloads, while the items would already be in the player's inventory. So:

| The chest is held by | Rummage |
|---|---|
| This game | Takes directly. |
| Another game, MultiUserChest loaded | Hands the take to MultiUserChest, which asks that game; that game removes the items and sends them back. |
| Another game, no MultiUserChest | Leaves the chest alone. |
| No game, or MultiUserChest ignores it | Leaves the chest alone. |

MultiUserChest takes over the same `MoveItemToThis` call through its `Inventory.AddItem` prefix, but only for an item it knows to be in that chest. Before each take Rummage checks that it does (`InventoryPatch.InventoryOfItem`), and after it that the chest is unchanged here and the target slot is held for the answer (`InventoryBlock.IsSlotBlocked`). A take that fails either check is not counted. A chest changed here although another game holds it is logged as an error.

MultiUserChest's answer arrives later. The next grab waits until every slot of the last one is released, or 10 seconds, whichever comes first. If the chest changed in the meantime, the holding game hands over what is now in that slot; if the player leaves before the answer, the answer is lost. Both are how MultiUserChest works for a drag too.

## Reaching the other mods

AzuCraftyBoxes, MultiUserChest and ExtraSlots are reached by type and member name at startup, not as build references. A member that cannot be found is logged once and its part is switched off: without AzuCraftyBoxes Rummage does nothing, without MultiUserChest other games' chests are left alone, and without ExtraSlots' check only the first four rows are filled.

## Self-test

`selftest/` is a plugin of its own, `Rummage.SelfTest`, built and deployed with a dev build and built in CI, never packaged. `/rummage selftest` in chat runs it where the player stands. It sets the player's belongings (all but what is equipped) aside in vault chests it builds, builds three test chests in front of the player and fills them with known items, and narrows Rummage's chests to those while it runs (`CraftyLink.OnlyChests`). Each part starts from refilled chests and an emptied inventory and uses the key's own entry points (`Context.Find`, `Grabber.Grab`, the search box's text and Enter). After every grab it checks that the inventory and the test chests hold together exactly what they held before.

Parts: a piece selected with the hammer, pressed twice; Leave One Item; chest rules (a private chest); a full inventory with one part stack; a recipe in the crafting tab, pressed twice; filling a stack; the search box; a chest handed to an owner that does not exist (MultiUserChest's request is never answered: the chest must not change here, the wait must end after its timeout, and the slot MultiUserChest holds is let go afterwards; the chest comes back to the player's game by itself, as the game gives a chest whose owner is not near to a player who is); and a chest handed to another player's game nearby, the way opening a chest hands it over, so their MultiUserChest answers and their game takes the items out. The last part is skipped when nobody else is within 30 m.

Everything is put back at the end: the player's belongings into the slots they came from, the test chests emptied and removed. What cannot be put back stays in its vault chest, which is left standing and reported with its place. Results are in `LogOutput.log` as `[selftest]` lines, ending with a `DONE` summary.

## Building

```
dotnet build                     # against the installed game, deploys to the Gale profile
dotnet build -p:LibsDir=lib      # against the stubs in lib/, as CI does
dotnet test tests/Rummage.Tests
```

`VALHEIM_DIR` or `-p:ValheimDir=` points at the game, `-p:ProfileDir=` at the BepInEx profile.
