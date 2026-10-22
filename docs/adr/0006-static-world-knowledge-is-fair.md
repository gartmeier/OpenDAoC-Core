# Static world knowledge is fair, dynamic knowledge comes only from Percepts

ADR 0001 limits an Agent to what a real game client knows. A real client ships with every zone's terrain and static geometry, and its Human has the in-game map. So an Agent may use static knowledge of the world, such as terrain height, walls, buildings and a navmesh built from them, without it reaching it as Percepts. Anything that changes while the server runs, such as where NPCs, players and doors are and what state they're in, still comes only from Percepts.

## Considered Options

- **No static knowledge**: the Agent learns the world only by walking into it. That's purer, but it knows less than a Human with the same client, and it can't even tell the height of the ground it walks on.

## Consequences

- The Harness and the Mind may use the server's navmeshes and zone data, but only their static parts: a navmesh query must not tell an Agent about a door's current state.
- The server writes each door's live state into its navmesh, and the flags on a path's nodes can show it. So the Harness queries the navmesh only with filters that don't depend on door state: for now, routes never go through doors at all. Routing through doors the Agent has seen open, from door Percepts, is a later option.
- Converting the zone-relative coordinates some packets carry into global ones, using the zone's static offsets, tells the Agent nothing a client doesn't know.
