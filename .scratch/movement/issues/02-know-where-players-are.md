# 02: Know where players are

**What to build:** The Mind knows where the other players near its Agent are, and keeps them in **Surroundings**. The Human runs around next to Greeen and sees the Mind print Percepts with the Human's position, in global coordinates, as it changes.

On the server:
- Player create and object delete get hand-written Percept projections, each with exactly the fields its 1.127 packet carries and naming the packet-library override it mirrors. Positions are global.
- Other players' movement reaches a client as a ready-built 1.127 player position packet passed to the packet library's raw send call, not as a typed call (see ADR 0001's consequences). That one layout is decoded into a typed Percept, mirroring the server code that builds it. Every other raw packet becomes a bare Percept naming its packet code.
- The generated TypeScript is regenerated.

In the Mind:
- A Surroundings module, fed Percepts. It keeps known players by object ID with name, position, heading, speed and when each was last heard from: created on player create, moved on player position, removed on object delete. It answers "who is called X" and "how far is this point from that one", working distances out when asked. It never forgets by time. Forgetting by distance from Self comes in ticket 05, NPCs in ticket 06.
- The entry point feeds every Percept to Surroundings. Surroundings knows nothing about echo or Come.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Player create gives a typed Percept with exactly what the 1.127 packet carries, positions global
- [ ] Object delete gives a typed Percept with the object ID
- [ ] The 1.127 player position packet given to the raw send call gives a typed Percept; a test feeds the server's own 1.127 builder output through the decoder and gets back what went in
- [ ] Any other raw packet gives a bare Percept naming its packet code
- [ ] The generated TypeScript is up to date and the codegen test passes
- [ ] Surroundings (`bun test`): create, move, delete; lookup by name; distance worked out on request; nothing forgotten by time
- [ ] In game: running around next to Greeen, the Mind prints the Human's positions as they change, and they match `/loc`
