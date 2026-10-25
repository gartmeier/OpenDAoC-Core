# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## Takeover leaves the new Mind's beliefs empty (2026-10-24)

The MCP server restarted mid-session, and its new Mind took over Dalta's connection (`HarnessClient.TakeOver`). The server sends only `entered`, never the state a login sends (player, skills, points, inventory, npcCreate for objects already in view). Self showed level 0 with no class, no specs, nothing equipped and an empty backpack, and Surroundings only had the 3 NPCs that came into view after the takeover. `/stats`, `/time` and a no-op moveItem didn't refresh anything.
Workaround: run more than 6000 units away and back to get npcCreate again. Level and specs come back on the next level up.
Needed: on a takeover, the server resends the full state a world entry sends.
After that takeover the Self also never learned its own object ID, so it ignored `revived` (and `died`; only a `status` with 0 health set it dead). `dead` stayed true after release, and `surroundings.attacking(me)` saw no attackers. Workaround: fed Self an `ownPosition` with the ID the `died` Percept named (#17736), then a `revived`.

## Debuffs on a Mob show only in chat (2026-10-24)

When Return Blade slowed a rock, only the chat said so ("can't attack as fast as normal", then "attacks return to normal"). A Human sees this only in chat too: the server sends no packet for the effects on a target. My own buffs and debuffs are in `agent.self.effects`.

## `train` can't train a line with a space in its name (2026-10-24)

`train(agent, "Celtic Dual", 3)` sends `/train Celtic Dual 3`, which the server ignores without a word, so it fails with `notTrained`. Sending `/train "Celtic Dual" 3` myself worked.
Needed: the porcelain quotes the line name.

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

## A `/train` right after another is sometimes ignored (2026-10-25)

At level 18, `trainBlades` sent `/train Blades 18`, then `/train "Parry" 12` and `/train "Celtic Dual" 12` one after the other, each waiting for its spec to change. Blades and Celtic Dual trained, Parry didn't, with no message at all; the same command a minute later worked.
Workaround: `trainBlades` now waits a second before each line and tries twice.
Needed: an answer from the server (or a Percept) when a `/train` is refused, so the Mind knows why.

## Constitution loss doesn't show anywhere (2026-10-25, fixed)

Fixed: `agent.self.constitutionLost` now shows it (it read 5 after my death at level 20).

## Max health stays stale after a Healer restores Constitution (2026-10-25, fixed)

Fixed in e5ac0e2e9: the Healer's stats packet already carries max health, and the `stats` Percept now passes it on, so `maxHealth` changes along with `constitutionLost`. Not yet seen in game.


Gralon (Howth) asked "It will cost 96 silver, 30 copper to have your lost constitution restored. Do you accept?" in a yes/no Dialog; `answer(agent, true)` worked, chat said "Gralon restores 5 points of your lost constitution." and `constitutionLost` went 5 → 0. But `agent.self.maxHealth` stayed 488 for 8 more seconds. Only when I sat down did a new `status` come with 496. The Healer (`Healer.cs`) sends only `SendCharStatsUpdate`, not a status update, so the Mind's max health waits for the next one.
Workaround: sit down (or do anything that changes health) and read `maxHealth` again.
Needed: the server sends a status update after the restore, or `stats` carries max health too. The Learning's snippet waits on `constitutionLost === 0` and then reads `maxHealth`, so it shows the old value.

Also: sending `/stand` as a command left me sitting; moving stood me up. Use the `stand` Primitive instead (it sends what the sit key sends).
