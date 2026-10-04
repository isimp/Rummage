# Rummage

Take what you need from the chests around you. Press J on a recipe selected in the crafting tab, on a piece in the build menu, or while placing a piece, and Rummage takes what it still costs, counting what you already carry. Press J over a stack in your inventory and it fills that stack to a full one. Press J anywhere else to open a search box over everything the chests hold, your build pieces and your recipes.

## AI notice

Most of Rummage was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

Each press brings enough for one more: one more piece than what you carry already pays for, or for a recipe one more round of the amount the crafting tab is set to make. A part the chests have run out of does not stop the count, so the other parts keep piling up while that one stays short. After a press, a line shows each part as what you now carry of what it needs, such as "Portal: Fine wood 10/10 (+10) · Surtling core 0/2". While building, the game's build panel shows under the piece what you carry of each part, in orange what the key would bring and in red what cannot be had. A stack is filled only with items of exactly its kind, and nothing goes anywhere else in your inventory. A recipe is taken for as many crafts as the crafting tab is set to make, and an upgrade for the next quality level. A recipe that needs only one of its ingredients takes the one you carry already, else the first the chests hold enough of.

In the search box, type part of a name and the best matches come first. The arrow keys choose, Tab completes the name and Escape closes. Enter on its own finds where the chosen entry is: the chests within 50 m holding it light up for a few seconds, and a line says how many there are, in how many chests and how far the nearest is. A number before or after the name, as in "40 wood" or "portal 2", makes Enter take that many instead; *Take* takes the slider's count. Items show how many the chests can give, pieces and recipes how many of them you could pay for. Under the list, the chosen entry shows what you carry and what the chests hold of each part against the count, with any part that falls short marked. Shift with J opens the search box on the recipe or piece in front of you, with its count filled in, instead of taking it straight away.

## How it decides

Rummage uses the chests AzuCraftyBoxes lets you craft from, within its range, and follows its rules for what may leave each chest. With its *Leave One Item* setting on, every chest keeps one of each item it holds, so storage mods that sort by what a chest already holds keep working. Chests you may not open, chests someone else has open and chests sealed by other mods are left alone.

What Rummage takes goes into your inventory only where it fits. Afterwards a few lines in the corner say what was taken and why anything is missing: kept back by *Leave One Item*, kept in by chest rules, in a chest someone has open, in someone's private chest (without saying how much), in a chest you may not take from, or not there at all. The chests it took from light up.

## Multiplayer

Each take goes through the same step as dragging an item out of a chest by hand. A chest another player's game looks after is used only when MultiUserChest is installed, which then asks that game for the items, the same way it does for a drag. Those items are reported as asked for, and once that game has answered, a second line says what arrived, or that it was turned down or never answered. Without MultiUserChest such chests are left alone. Rummage itself is only needed by the players who use it.

## Settings

The key and the lines in the build panel (on or off) are in `BepInEx/config/isimp.Rummage.cfg`. Alt with the same key is left to other mods.

Tested with AzuCraftyBoxes 1.8.27, MultiUserChest 0.6.2, ExtraSlots 1.2.16 and AzuAntiArthriticCrafting 2.1.11.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rummage
