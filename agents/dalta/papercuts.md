# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## Takeover leaves the new Mind's beliefs empty (2026-10-24)

The MCP server restarted mid-session, and its new Mind took over Dalta's connection (`HarnessClient.TakeOver`). The server sends only `entered`, never the state a login sends (player, skills, points, inventory, npcCreate for objects already in view). Self showed level 0 with no class, no specs, nothing equipped and an empty backpack, and Surroundings only had the 3 NPCs that came into view after the takeover. `/stats`, `/time` and a no-op moveItem didn't refresh anything.
Workaround: run more than 6000 units away and back to get npcCreate again. Level and specs come back on the next level up.
Needed: on a takeover, the server resends the full state a world entry sends.
After that takeover the Self also never learned its own object ID, so it ignored `revived` (and `died`; only a `status` with 0 health set it dead). `dead` stayed true after release, and `surroundings.attacking(me)` saw no attackers. Workaround: fed Self an `ownPosition` with the ID the `died` Percept named (#17736), then a `revived`.

## Buffs and debuffs are invisible (2026-10-24)

A Hastener's speed buff lasts 10 minutes and ends in a fight, but I couldn't tell: a Human sees it in the buff bar with its time left. The server sends the bar (`SendUpdateIcons` in the Journal), yet it arrives only as a bare Percept with no contents. All I saw was `maxSpeed` going 191 → 281.
The same goes for debuffs, on me or on a Mob: when Return Blade slowed a rock, only the chat said so ("can't attack as fast as normal", then "attacks return to normal"), and a debuff a Mob puts on me would show the same way, if at all.
Workaround: watch `agent.self.maxSpeed` for the speed buff, and the chat for the rest.
Needed: a Percept for my buff bar (buffs and debuffs, each with its name and time left) kept on the Self, plus whatever a Human sees of the effects on their target.

## `train` can't train a line with a space in its name (2026-10-24)

`train(agent, "Celtic Dual", 3)` sends `/train Celtic Dual 3`, which the server ignores without a word, so it fails with `notTrained`. Sending `/train "Celtic Dual" 3` myself worked.
Needed: the porcelain quotes the line name.

## Healer's Constitution dialog can't be answered (2026-10-25)

After dying, "You've lost some constitution, go to a healer". `talkTo` the Healer Ionhar cured resurrection sickness, then the server sent its "It will cost ... Do you accept?" custom dialog (`Healer.cs`, `SendCustomDialog`), which reached the Mind as nothing and has no Primitive to accept. Max health stayed 172 instead of 179.
Needed: a Percept for custom dialogs (text) and a Primitive to accept or decline them.

## Routes stop at X≈347100 east of Howth (2026-10-25)

Every `runTo` into X:349000–358000 Y:590000–607000 (large eirebugs, cluricaun trips, sheevra) ends `unreachable` at exactly X:347100 Z:4613, from the north, the west and the south. A Human could walk there, as far as I know. Maybe the navmesh is missing tiles there.
Needed: a navmesh there, or to know it's not walkable.

## `kill` times out on the run to a far Mob (2026-10-25)

`kill` starts its 30 s `noDamage` clock before running to the Mob, so a Mob more than ~5500 units away failed with `noDamage` while I was still running, every time (`grind` then tried again from closer and won).
Workaround: `grind` runs within 800 units of a Mob more than 2000 away before calling `fight`.
Needed: the clock starts once in melee range, or `kill` fails with a separate reason while the run is going.

## `/task` answers in a window the Mind can't see (2026-10-25)

`/task` shows the task, its expiry time and how many tasks I've done this level in a custom text window (`SendCustomTextWindow`), which never reaches the Mind. I only learnt a task had expired when its hand-in gave nothing.
Needed: a Percept with the text of custom text windows.

## No way to use an item (2026-10-25)

The backpack has a Personal Bind Recall Stone, which a Human uses (right-click or a quickbar slot) to go back to the Bind point. A Kill Task loop that runs 16000 units each way could recall after the kill instead. There's no Primitive to use an item, and no `/use` command on the server.
Needed: a `useItem` Primitive for a backpack or equipment slot.
