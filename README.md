# Rummage

Take what a build piece costs from the chests around you with one key. Hover a piece in the build menu, or hold the one you are placing, and press J. Rummage counts what you already carry and takes the rest from the chests within reach.

## AI notice

Most of Rummage was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## How it decides

Rummage uses the chests AzuCraftyBoxes lets you craft from, within its range, and follows its rules for what may leave each chest. With its *Leave One Item* setting on, every chest keeps one of each item it holds, so storage mods that sort by what a chest already holds keep working. Chests you may not open, chests someone else has open and chests sealed by other mods are left alone.

What Rummage takes goes into your inventory only where it fits. Afterwards a few lines in the corner say what was taken and why anything is missing, and the chests it took from light up.

## Multiplayer

Each take goes through the same step as dragging an item out of a chest by hand. A chest another player's game looks after is used only when MultiUserChest is installed, which then asks that game for the items, the same way it does for a drag. Without MultiUserChest such chests are left alone. Rummage itself is only needed by the players who use it.

## Settings

The key is in `BepInEx/config/isimp.Rummage.cfg`. Alt with the same key is left to other mods.

Tested with AzuCraftyBoxes 1.8.27, MultiUserChest 0.6.2 and ExtraSlots 1.2.16.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rummage
