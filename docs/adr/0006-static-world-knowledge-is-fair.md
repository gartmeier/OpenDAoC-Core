# Static world knowledge is fair, dynamic knowledge comes only from Percepts

ADR 0001 limits an Agent to what a real game client knows. A real client ships with every zone's terrain and static geometry, and its Human has the in-game map. So an Agent may use static knowledge of the world, such as terrain height, walls, buildings and a navmesh built from them, without it reaching it as Percepts. Anything that changes while the server runs, such as where NPCs, players and doors are and what state they're in, still comes only from Percepts.

## Considered Options

- **No static knowledge**: the Agent learns the world only by walking into it. That's purer, but it knows less than a Human with the same client, and it can't even tell the height of the ground it walks on.

## Consequences

- The Harness and the Mind may use the server's navmeshes and zone data, but only their static parts: a navmesh query must not tell an Agent about a door's current state.
- The server writes each door's live state into its navmesh, and the flags on a path's nodes can show it. So the Harness queries the navmesh only with filters that don't depend on door state.
- Routes go through doors that open by clicking, such as a town building's. The server marks those as doors whether they're open or closed, so routing through them says nothing about their state. It marks only a closed door that doesn't open by clicking, such as a keep door, as blocking, and routes avoid that one. Such a door is routed through while it's open, which tells an Agent its state. There are none where Agents play so far.
- The Agent walks through a door that opens by clicking even while it's closed. The server doesn't stop it, though a Human's client would until they clicked it open. Opening closed doors on the way with the client's door request, from door Percepts, is a later option.
- Converting the zone-relative coordinates some packets carry into global ones, using the zone's static offsets, tells the Agent nothing a client doesn't know.
- Game data a Human could read on a fan wiki, such as where Mobs spawn, their levels and what merchants sell, is static too. It reaches the Agent as Wiki documents generated from the server's database, as rs-sdk's wiki is generated from its game's content (ADR 0009). Where a Mob or NPC is right now still comes only from Percepts.
