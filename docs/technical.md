# Technical notes

## Layout

`src/Core` decides a grab and has no Unity or BepInEx reference, so `tests/Rummage.Tests` builds it on its own. `src/Game` reads the game and the other mods into the core's terms and carries out the result. `src/Plugin.cs` holds the key.

## A grab

1. **What for.** The build piece under the cursor in the build menu (`Hud.m_hoveredPiece`), else the piece being placed (`Player.GetSelectedPiece`). Its cost is a total to carry: what the inventory already holds counts, and only items at or above the world level count, as building only takes those.
2. **Where from.** AzuCraftyBoxes' `API.GetNearbyContainers` at its own `Container Range`. That list already leaves out chests the player may not open, warded chests, chests open in another game and chests its own toggles exclude; mods that seal chests from automation keep them out of it. Entries that are not vanilla chests (item drawers, the player's bags) are skipped.
3. **The plan** (`Core/Planner.cs`). Nearest chest first. A chest's rules (`API.CanItemBePulled` with the chest's and the item's prefab names) can keep an item in. With AzuCraftyBoxes' `Leave One Item` on, each chest keeps one unit of each item it holds, counted by item rather than by stack, as AzuCraftyBoxes counts it. Each take is sized to land whole in one inventory slot: a part stack of exactly the same kind first (same item and world level, and the same quality for items with quality levels), then an empty slot. No slot is the target of two takes in one grab. ExtraSlots' own slots are never targets.
4. **The takes** (`Game/Grabber.cs`). Each one is `Inventory.MoveItemToThis(chest, item, amount, x, y)`, the call the inventory screen makes for a drag. It adds to the inventory and lowers the chest's stack in the same step, so an item is never in both places.
5. **The report** (`Core/Report.cs`): what was taken, and for each shortfall the units kept back by `Leave One Item`, kept in by chest rules, in chests out of reach, or that there was no room.

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

## Building

```
dotnet build                     # against the installed game, deploys to the Gale profile
dotnet build -p:LibsDir=lib      # against the stubs in lib/, as CI does
dotnet test tests/Rummage.Tests
```

`VALHEIM_DIR` or `-p:ValheimDir=` points at the game, `-p:ProfileDir=` at the BepInEx profile.
