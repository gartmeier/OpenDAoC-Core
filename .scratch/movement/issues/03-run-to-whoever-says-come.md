# 03: Run to whoever says `come`

**What to build:** The tracer bullet. The Human says `come` next to Greeen, and he runs to a point 100 units short of where the Human stood, under exactly the rules a Human's client runs under. `stop` stops him. Ctrl+C during a run stops him and then quits.

On the server:
- `moveTo(x, y, z)` and `stop` Primitives. `moveTo` runs in a straight line at the maximum speed the server last told the client. A new `moveTo` replaces the current one. Results report only whether they were queued (ADR 0004).
- A pure stepping class, like the liveness one. Given start, target, speed and elapsed game time, it gives the position, heading and Z to send, and whether the target is reached. Z is interpolated linearly from start to target, the heading points at the target, it never overshoots after a stall, and the zone ID is the zone at the current X and Y in the region's static layout (ADR 0006).
- While running, the Harness sends running position updates (ticket 01's builder) at the running rate from the capture. Standing, it falls back to the idle liveness unchanged. Only while a Mind is connected (ADR 0005).
- Percepts the Harness works out itself:
  - `moved` with each position update while running: position, heading, speed;
  - `arrived` with where the Character ended up;
  - `stopped` with where it ended up and the reason: `stop`, or replaced by a new `moveTo`.
- Projections for the Agent's own position at world entry and for the maximum speed update.
- Regenerated TypeScript.

In the Mind:
- A Self module, fed Percepts: own position, heading, maximum speed, and a single movement status (standing, moving to a target, arrived, stopped with a reason), built only from Percepts. Type name `Self`, never a variable named `self`.
- A Come module. A say line from someone else whose whole text is `come` (case-insensitive) gives a `moveTo` to the point 100 units short of the speaker's position in Surroundings, along the line from Self. A speaker not in Surroundings gives nothing. `stop` from someone else gives `stop`. Anything else gives nothing.
- The connection's Ctrl+C sends `stop` and then `quit`, because the server refuses `/quit` while the Character is moving.
- The entry point runs the Come Mind. The echo module and its tests are deleted.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [ ] Stepping (server unit tests): exact arrival, heading towards the target, Z interpolation, no overshoot after a stall, a stop mid-run, the zone ID changing at a zone border within a region
- [ ] Own position at world entry and maximum speed give typed Percepts
- [ ] Self (`bun test`): status through `moved`, `arrived`, `stopped`; maximum speed
- [ ] Come (`bun test`): another speaker's `come` gives the right `moveTo`; an unknown speaker, own lines, other chat types and longer lines give nothing; `stop` gives `stop`
- [ ] Connection (`bun test`): Ctrl+C sends `stop` before `quit`
- [ ] In game: `/say come` from about 30 m on flat ground makes Greeen run, smoothly in the Human's client with the running animation, to about 100 units short of the Human; the Mind prints `arrived`
- [ ] In game: `/say come` then `/say stop` stops him mid-run; the Mind prints `stopped` with the stop reason
- [ ] In game: Ctrl+C during a run stops him, and he quits through the normal path
- [ ] The server log has no speed-check entries for Greeen after these runs
