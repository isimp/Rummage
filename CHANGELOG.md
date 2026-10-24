# Changelog

## 0.3.1

When a grab is short of something that chests further away still hold, within the search box's 50 metres but past the range Rummage takes from, a line after the shortfall says how many are there, in how many chests and how far the nearest is, and those chests light up.

Chests a find lights up get a short ping on the map, with the game's own ping icon, named on the large map by what they hold. The pings are yours alone, are never saved and go away after a few seconds.

Finding and the search box also see a chest built or loaded a moment ago, before AzuCraftyBoxes has looked for chests again.

`FindGlow`, `TakeGlow` and `PingSeconds` set how long chests stay lit after a find and after a grab, and how long the pings stay. 0 turns each off.

## 0.3.0

`PiecePress` decides how a press for a build piece counts what you carry. `TopUp`, the default, works as before. With `FullSet` every press takes the piece's whole cost whatever you carry, so presses for different pieces add up, and a count typed in the search box for a piece brings that many whole sets. Recipes still count what you carry.

The search box lists the last two things you found or took first when nothing is typed, marked with circling arrows. Enter again on something you just found takes one of it, until you take it or find something else, and Shift with Enter finds it again. They are kept for each character, `/rummage forget` clears them, and `RememberLastUsed` turns this off.

An item the search box lists that no chest nearby holds any more reads *none nearby*.

## 0.2.0

*New items* takes one of every item in the chests nearby you have never carried, which unlocks the recipes and pieces that use them. It is the search box's first row whenever there is something new, and `/rummage new` in chat does the same.

Short sounds from the game's interface play when the search box opens or closes, when the key takes something and when finding lights up chests. `PlaySounds` turns them off.

The search box no longer has a heading, so the results start higher.

Items whose names another mod colours, such as CreatureBait's *Boar Bait*, are found by typing their name, and Tab completes them without the colour markup.

## 0.1.0

First release.
