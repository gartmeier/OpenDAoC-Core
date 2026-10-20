# 06: See NPCs

**What to build:** The Mind knows the NPCs around its Agent and keeps them in Surroundings alongside players.

- NPC create and object update get hand-written Percept projections, each with exactly the fields its 1.127 packet carries and naming the override it mirrors. The object update's zone-relative coordinates are converted to global ones using the zone's static offsets (ADR 0006).
- Surroundings holds NPCs too: created on NPC create, updated on object update, removed on object delete.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] NPC create gives a typed Percept with exactly what the 1.127 packet carries, positions global
- [ ] Object update gives a typed Percept with position (converted to global), heading, speed, target and health
- [ ] Surroundings (`bun test`): NPC create, update and delete
- [ ] In game: the NPCs near Greeen show up in the Mind's output with positions that match their `/loc` when targeted
- [ ] In game: someone Greeen can't see says `come` and nothing happens (needs ticket 03; check it once both are done)
