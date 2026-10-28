# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

- After Orla and I both died and released to Connla ("Your surroundings suddenly change!"), she dropped out of my Surroundings for good: the server kept sending `playerPosition` for her object ID (the journal shows `#21221 (unknown)`), but never her `playerCreate` again, and Surroundings ignore positions of objects they don't know. She couldn't see me either. Running 8000 away and back didn't bring her back. A real client asks the server for an object it gets updates for but doesn't know; I'd have needed the Mind to do that. Workaround: `mate()` in tactics/group.ts stands in with her object ID and the group map's position; targeting and casting by ID work. (Before, after a server restart, the same happened and a relog fixed it.)
- A long `/g` line is cut off at about 200 characters: keep group lines short.
- A 15 min `execute_code` that hit its timeout during taskLoop's run east left me relogged ("Fearnan wasn't in the world, and has entered it first"): out of the group and without the Pet. Workaround: `/invite Orla` and `upkeep` again; keep runs well under the timeout.
- No way to hand an item to a group member: no trade Primitive, and loot she drops at my feet is "doesn't belong to you". A real client can trade. I'd need a trade Primitive (or `/trade`).
- I can see only my own Effects (`agent.self.effects`), not my Pet's: a real client's pet window shows the Pet's buff icons. To know whether Ceol's base buffs are on the lynx I'd need the Pet's Effects; workaround: assume a grouped Bard covers it and remember "already has that effect" refusals.
- A snippet waiting out `/quit`'s 20 s sit is aborted by any /g line, and the abort stands me up, which cancels the quit ("You are no longer waiting to quit"). Twice at the bindstone. Workaround: send `/quit` again once the group has gone quiet. I'd need aborts not to stand me up while a quit is pending.
