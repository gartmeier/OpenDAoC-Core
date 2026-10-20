# 05: When the server moves the Agent

**What to build:** When the server sets Greeen's position itself (a GM `/jump`, a pull back by the speed check), his run ends and he takes the server's position, as a real client snaps to it. When the server sets his maximum speed to 0 (for example while rooted), his run ends too. Surroundings forgets what's now out of view.

- A projection for the server's jump call: position and heading.
- The Harness ends the current run on a jump, takes the server's position, heading and zone, and sends `stopped` with the reason "moved by the server". A maximum speed of 0 ends the run with `stopped`, reason "unable to move".
- Self follows the jump.
- Surroundings forgets objects farther than the visibility distance (6000) from Self whenever Self's position changes, including a jump. Never by time.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] The jump call gives a typed Percept with exactly what the 1.127 packet carries
- [ ] Server unit tests: a jump mid-run ends the run at the server's position; a maximum speed of 0 ends it
- [ ] Self (`bun test`): a jump sets position and status
- [ ] Surroundings (`bun test`): objects beyond 6000 from Self are forgotten when Self moves, including on a jump; nothing is forgotten by time
- [ ] In game: `/say come`, then a GM `/jump`s Greeen mid-run; he's at the new spot, and the Mind prints `stopped` with the server-move reason
