# Rummage

Take what you need from the chests around you with one key. Press J over a recipe, a build piece or a stack in your inventory, and Rummage takes what it still needs, counting what you already carry. Press J anywhere else to search the chests, find where something is and take any amount of it.

**Rummage needs AzuCraftyBoxes 1.8.27 or later**, which decides which chests Rummage may use and what may leave them. Mod managers install it along with Rummage; without it, Rummage does not load.

![The Rummage search box](https://raw.githubusercontent.com/isimp/Rummage/main/docs/images/screenshot.webp)

## AI notice

Most of Rummage was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Getting started

While building, select or hover a piece and press J to take what it costs. In the crafting tab, press J on a recipe to take what it needs for as many crafts as the tab is set to make, or for the next quality level of an upgrade; a recipe that needs only one of its ingredients takes the one you carry already, else the first the chests hold enough of. With the inventory open, press J over a stack to fill it to a full one, with items of exactly its kind and nothing put anywhere else.

Each press brings enough for one more: one piece more than what you carry already pays for, or one more round of the crafting amount. A part the chests have run out of does not stop the count, so the other parts keep piling up while that one stays short. While building, a box beside the build panel shows what you carry of each part, in orange what the key would bring and in red what cannot be had.

## The search box

Press J with nothing in front of you, or Shift with J to start from the recipe or piece you are looking at with its count filled in. Type part of a name and the closest matches come first; the arrow keys choose and Tab completes the name.

Enter on its own finds where the chosen entry is: the chests within 50 m holding it light up for a few seconds, and a line says how many there are, in how many chests and how far the nearest is. A number before or after the name, as in "40 wood" or "portal 2", makes Enter take that many instead, unless the number is part of a name you typed in full, such as a piece called "Banner 2"; then "2 Banner 2" takes two. *Take* takes the count on the slider, and Escape closes the box.

Items show whether any of them can be taken, so something the chests keep back can still be found. Pieces and recipes show how many of them what you carry and what the chests hold would pay for, and under the list the chosen entry shows each part as carried and in the chests against the count.

## How it decides

Rummage uses the chests AzuCraftyBoxes lets you craft from, within its range, and follows its rules for what may leave each chest. With its *Leave One Item* setting on, every chest keeps one of each item it holds, so storage mods that sort by what a chest already holds keep working. Chests you may not open, chests someone else has open and chests sealed by other mods are left alone.

What Rummage takes goes into your inventory only where it fits. Afterwards a few lines say what was taken and why anything is missing: kept back by *Leave One Item*, kept in by chest rules, in a chest someone has open, in someone's private chest (without saying how much), in a chest you may not take from, or not there at all. The chests it took from light up. While the inventory is open, which covers the corner the lines usually appear in, they show in the middle of the screen.

## Multiplayer

Each take goes through the same step as dragging an item out of a chest by hand. A chest another player's game looks after is used only when MultiUserChest is installed, which then asks that game for the items, the same way it does for a drag. Those items are reported as asked for, and once that game has answered, a second line says what arrived, or that it was turned down or never answered. Without MultiUserChest such chests are left alone. Rummage itself is only needed by the players who use it, and servers do not need it.

## Settings

The key and the box beside the build panel are in `BepInEx/config/isimp.Rummage.cfg`, each with a description. Alt with the same key is left to other mods.

Tested with AzuCraftyBoxes 1.8.27, MultiUserChest 0.6.2, ExtraSlots 1.2.16 and AzuAntiArthriticCrafting 2.1.11.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rummage
