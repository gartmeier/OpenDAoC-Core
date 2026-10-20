# 04: Face the speaker

**What to build:** When Greeen arrives after a `come`, he turns to face whoever called him. If the speaker is already within 100 units, he only turns.

- A `face(heading)` Primitive, sent as the heading update a real client sends (ticket 01's builder). A `face` during a run is immediately overtaken by the run's own heading and doesn't end the run.
- The Come module: on `arrived` after a `come`, it gives a `face` towards that speaker's last known position in Surroundings. A speaker within 100 units of Self gives a `face` instead of a `moveTo`.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] `face` sends the heading update bytes the capture shows (golden bytes via ticket 01's builder)
- [ ] Come (`bun test`): `arrived` after `come` gives a `face` towards the speaker; a speaker within 100 units gives a `face` only
- [ ] In game: after `/say come`, Greeen arrives and turns to face the Human
- [ ] In game: `/say come` from right next to him makes him turn without moving
