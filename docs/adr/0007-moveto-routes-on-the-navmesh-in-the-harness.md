# `moveTo` routes along the navmesh in the Harness

A real game client has no pathfinding: its Human picks the route and the client only keeps the Character on the ground. Even so, the Harness's `moveTo` follows a route it works out on the server's navmesh, keeps Z on the ground at every step, and moves an unwalkable target to the nearest walkable point. In a zone without a navmesh it runs in a straight line, as before. The navmesh exists only inside the server as native code, and the Mind's need is "get to this remembered point". So routing in the Harness was one change, reusing what the server already does for NPCs, and every Tactic gets it for free.

This is the one place where a Primitive does more than a client would. The route uses only static knowledge (ADR 0006), and the position updates still go through the real handlers and the speed check (ADR 0002), so the Agent knows nothing a Human with a map couldn't, and moves no faster.

## Considered Options

- **Path query, walking in the Mind**: the Mind asks the Harness for a route and sends a straight `moveTo` per leg. That keeps Primitives one-to-one with a client, but needs a new request-and-reply shape in the protocol (ADR 0004 only covers dispatch results) and makes every Tactic walk routes itself. It can still be added later if a Mind needs to see a route before committing to it.
- **Navmesh in the Mind**: the Mind loads the navmeshes itself. That would put the native Detour library into the TypeScript process.

## Consequences

- A target the route can't reach ends the run with `stopped` and the reason `unreachable`, after running as far as the route gets.
- The route never goes through doors (ADR 0006).
