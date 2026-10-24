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
