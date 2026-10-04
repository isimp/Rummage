# Rummage

Take what you need from the chests around you with one key. Press J over a recipe, a build piece or a stack in your inventory, and Rummage takes what it still needs. Press J anywhere else to search the chests.

**Rummage needs AzuCraftyBoxes 1.8.27 or later.** Mod managers install it along with Rummage.

![The Rummage search box](https://raw.githubusercontent.com/isimp/Rummage/main/docs/images/screenshot.webp)

## AI notice

Most of Rummage was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Getting started

Each press of J brings enough for one more piece or craft than you already carry, and fills a stack in your inventory to a full one. While building, a box beside the build panel shows what you carry and what J would bring.

In the search box, Enter finds where something is and lights up its chests; type a number with the name, as in "40 wood", and Enter takes that many.

## Good to know

Rummage follows AzuCraftyBoxes' rules: only chests you may use, within its range, and with *Leave One Item* on, one of each item stays in every chest. Chests you could not open by hand are left alone. Other players' chests are used through MultiUserChest when it is installed, the same way it handles dragging an item out by hand.

Rummage runs on your own machine only; servers do not need it.

## Settings

The key and the box beside the build panel are in `BepInEx/config/isimp.Rummage.cfg`.

Tested with AzuCraftyBoxes 1.8.27, MultiUserChest 0.6.2, ExtraSlots 1.2.16 and AzuAntiArthriticCrafting 2.1.11.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rummage
