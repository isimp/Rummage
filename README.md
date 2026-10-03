# Rummage

Take what you need from the chests around you. Press J on a recipe selected in the crafting tab, on a piece in the build menu, or while placing a piece, and Rummage takes what it still costs, counting what you already carry. Press J over a stack in your inventory and it fills that stack to a full one. Press J anywhere else to open a search box over everything the chests hold, your build pieces and your recipes.

## AI notice

Most of Rummage was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

Each press brings enough for one more: one more piece than what you carry already pays for, or for a recipe one more round of the amount the crafting tab is set to make. A stack is filled only with items of exactly its kind, and nothing goes anywhere else in your inventory. A recipe is taken for as many crafts as the crafting tab is set to make, and an upgrade for the next quality level. A recipe that needs only one of its ingredients takes the one you carry already, else the first the chests hold enough of.

In the search box, type part of a name and the best matches come first. The arrow keys choose, Tab completes the name, Enter takes and Escape closes. A number before or after the name sets the count, as in "40 wood" or "portal 2", or use the slider. Items show how many the chests can give, pieces and recipes how many of them you could pay for. Shift with J opens the search box on the recipe or piece in front of you instead of taking it straight away.

## How it decides

Rummage uses the chests AzuCraftyBoxes lets you craft from, within its range, and follows its rules for what may leave each chest. With its *Leave One Item* setting on, every chest keeps one of each item it holds, so storage mods that sort by what a chest already holds keep working. Chests you may not open, chests someone else has open and chests sealed by other mods are left alone.

What Rummage takes goes into your inventory only where it fits. Afterwards a few lines in the corner say what was taken and why anything is missing, and the chests it took from light up.

## Multiplayer

Each take goes through the same step as dragging an item out of a chest by hand. A chest another player's game looks after is used only when MultiUserChest is installed, which then asks that game for the items, the same way it does for a drag. Without MultiUserChest such chests are left alone. Rummage itself is only needed by the players who use it.

## Settings

The key is in `BepInEx/config/isimp.Rummage.cfg`. Alt with the same key is left to other mods.

Tested with AzuCraftyBoxes 1.8.27, MultiUserChest 0.6.2, ExtraSlots 1.2.16 and AzuAntiArthriticCrafting 2.1.11.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rummage
