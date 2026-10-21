# 06: See NPCs

**What to build:** The Mind knows the NPCs around its Agent and keeps them in Surroundings alongside players.

- NPC create and object update get hand-written Percept projections, each with exactly the fields its 1.127 packet carries and naming the override it mirrors. The object update's zone-relative coordinates are converted to global ones using the zone's static offsets (ADR 0006).
- Surroundings holds NPCs too: created on NPC create, updated on object update, removed on object delete.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 02

**Status:** resolved

- [x] NPC create gives a typed Percept with exactly what the 1.127 packet carries, positions global
- [x] Object update gives a typed Percept with position (converted to global), heading, speed, target and health
- [x] Surroundings (`bun test`): NPC create, update and delete
- [x] In game: the NPCs near Greeen show up in the Mind's output with positions that match their `/loc` when targeted
- [x] In game: someone Greeen can't see says `come` and nothing happens (needs ticket 03; check it once both are done)

## Comments

**2026-10-20 (agent):** Built on branch `claude/m3-06-see-npcs-51d9f0` (`af18961e6`, `f8ac0f2d5`). The in-game boxes need the Human, so this is back with the Human.

What exists now:
- **NPC create Percept** (`npcCreate`). Mirrors `PacketLib1124.SendNPCCreate` (1.125 to 1.127 don't override it): global `x`, `y`, `z`, `objectId`, `speed` and `zSpeed` (0 unless it's on its way somewhere), `heading`, `model`, `size`, the display `level`, `statue`, `realm`, the `ghost`, `equipped`, `torch`, `peaceful`, `flying`, `pet`, `untargetable`, `nameHidden` and `stealthed` flags, `questIndicator`, `name` and `guildName`, cut as the packet cuts them. A GM sees no hiding flags but `[name]-DOR-NON`. Nothing for an NPC the Agent can't see or an enemy's mine; a moving object (boat, siege weapon, mine) gives a bare `SendMovingObjectCreate`.
- **Object update Percept** (`objectUpdate`). Mirrors `PacketLib168.SendObjectUpdate`: `x`, `y`, `z`, `objectId`, `speed`, `zSpeed` (a quarter of the vertical speed, signed), `heading`, `targetId` (0, the target, or 65535 while attacking from out of reach), `healthPercent`, `realm`, `untargetable` (a statue too), `nameHidden`, `underwater`, `flying`, `zoneId` (the zone's skin ID), and `destination` (global, only while the NPC is on its way to a known point). The packet's zone-relative coordinates are turned back into global ones with the zone's offsets (ADR 0006), keeping the packet's 16-bit truncation.
- **Protocol** regenerated (it now also lists `eQuestIndicator`).
- **Surroundings: one collection.** Players and NPCs share the server's object ID range, and a delete or remove Percept doesn't say which it was, so Surroundings keeps one map of `KnownObject`s by ID, each with `kind` `"player"` or `"npc"`, plus `name`, `position`, `heading`, `speed`, `lastHeardMs`. `objects`/`object(id)` cover both, `players`/`player(id)` and `npcs`/`npc(id)` one kind, and `named(name)` finds either. NPC create makes an NPC known, object update moves any known object, delete and remove forget. Updates for unknown objects are ignored. Nothing is forgotten by time; forgetting by distance comes with ticket 05.
- **Printing:** `npcCreate #1110 rat	X:531234 Y:478901 Z:2345 heading 3072 speed 0 level 3` and `objectUpdate #1110 rat	X:... heading ... speed ... zone 101 health 100%` (plus ` target #id` and ` to X:.. Y:.. Z:..` when present).
- Tests: server 211 passing in Release (21 new projection cases incl. the zone-offset conversion, checked by mutation; 2 protocol writes). Mind 36 passing in `bun test`, `bun run typecheck` clean.

For the Human, on a server built from this branch:
1. `bun mind/agent.ts greeen`. When Greeen enters the world, the Mind prints `npcCreate` lines for the NPCs around him.
2. Target one of those NPCs with your GM Character and type `/mob info`. Its `Position (X, Y, Z, H)` matches the `npcCreate` line with the same name (X, Y, Z and heading). `/loc` prints zone coordinates, so use `/mob info` or `/gloc` beside it.
3. Wait about 5 s: `objectUpdate #<same id> <name>` lines come for it, with the same position while it stands. For a roaming NPC, the last `objectUpdate` before it stops matches `/mob info` once it stands still.
4. Kill or despawn an NPC near Greeen (`/mob remove` on a test mob, for example). The Mind prints `objectRemove #<id> type ...`.
5. The last box (someone Greeen can't see says `come`, nothing happens) needs ticket 03's Come Mind too. Check it once 03 and this branch are both merged.

Known gaps:
- The steed's riding packet and the PvP guild-ID hack that the library sends after an NPC create are written inside the library and never reach the Agent.
- NPC name translations (`LanguageMgr`) are mirrored but untested; the tests have no translations loaded.
- The real `targetId` (NPC standing within reach of its target) is untested: `AttackRange` needs a real attack component. 0 and 65535 are tested.
- Moving objects stay bare Percepts.

**2026-10-21 (Human, recorded by agent):** Ran the checks above on a server built from this branch with main merged in (`d02923d07`, so ticket 03's Come Mind runs too). The Mind's output, read from the terminal:
- At 0.28 s the Mind printed 207 `npcCreate` lines for the NPCs around Greeen in zone 202, then `objectUpdate` lines for the same IDs about every 5 s. Standing NPCs keep their position. Roaming ones show `speed 70` and a ` to X:… Y:… Z:…` destination, then stand again where it said.
- The Guardian's `/mob info` gave `295856, 642418, 4848, 713`. The Mind had `npcCreate #1400 Guardian	X:295856 Y:642418 Z:4848 heading 713 speed 0 level 42` at 0.28 s and the same position in every `objectUpdate #1400` up to 415 s. A first try with roaming mudmen couldn't be compared: they move every 30 s or so, and the terminal keeps only about 20 s of this output. Use an NPC that doesn't roam, or `bun mind/agent.ts greeen | tee percepts.log`.
- Killing a beach rat gave `objectRemove #2168 type 0` (a dead NPC), so Surroundings forgets it. It respawned later with the same object ID.
- GM-stealthed, the Human was forgotten (`objectDelete #17736`), then `/say come` reached the Mind as `Vue says, "come"` at 230.9 s and Greeen didn't move: no `moved`, `arrived` or `stopped` followed.
- `#7999` is an NPC with an empty name, one unit above Channeler Glasny. It's what the server sends, not a gap.

All acceptance items are met.
