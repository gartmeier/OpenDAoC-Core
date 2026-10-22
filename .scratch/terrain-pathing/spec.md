# Milestone 3b: Terrain and pathing

Status: ready-for-agent

## Problem Statement

Milestone 3 lets an Agent run, but only in a straight line with Z interpolated between start and target. On a hill its Character floats or sinks, and a run goes straight through walls, buildings, trees and water. That was fine on flat open ground for the acceptance test. Milestone 4's grind Tactic, though, will send the Agent to mobs and to coordinates it remembers across real terrain. Before it can, I need an Agent that stays on the ground, goes around what's in the way, and says so when it can't get somewhere.

## Solution

The server already has Detour pathfinding, the same one NPCs use, but neither the native library nor any navmeshes are installed. A script builds both for Hibernia's home region (region 200, all 14 zones) from the game client's zone data, using OpenDAoC-BuildNav.

The Harness's `moveTo` then follows a route on the zone's Navmesh instead of a straight line, keeps the Character on the ground at every step, and moves a target that isn't walkable to the nearest point that is (ADR 0007). Routes avoid water, drops, doors and ladders. A new optional `within` ends the run that many units short of the end of the route. A target the route can't reach ends the run with `stopped` and the new reason `unreachable`. A new `routed` Percept tells the Mind the route each time one is worked out. In a zone without a Navmesh, runs stay straight lines as before.

The Come Mind sends `moveTo` to the speaker with `within: 100` and turns to face them on `arrived`.

## User Stories

1. As the Human, I want to say `come` from the top of a hill and have the Agent run up it on the ground, so that his Character never floats or sinks in my client.
2. As the Human, I want to say `come` from behind a building or wall and have the Agent run around it, so that he never runs through solid things.
3. As the Human, I want to say `come` from across a river and have the Agent use a bridge or ford, so that he never runs across water.
4. As the Human, I want the Agent to never run off a drop, so that he never falls, which an Agent can't do yet.
5. As the Human, I want the Agent to stop about 100 units short of me along his route, so that he stands next to me on my side of any wall.
6. As the Human, I want a `come` from where the Agent can't get to, like deep water or a roof, to bring him as close as the ground allows, so that he still tries.
7. As the Human, I want routes to work across the zone borders inside region 200, so that a `come` near a border isn't a dead end.
8. As the Human, I want routes never to use doors, so that the Agent never runs through a closed one and never learns a door's state from the Navmesh (ADR 0006).
9. As the Human, I want routes never to use ladders or jump links, so that the Agent only moves in ways the Harness can do.
10. As the Human, I want a routed run to pass the server's speed check exactly as a straight run does, so that Agents still provably move under the same rules as Humans.
11. As the Human, I want routed runs to go through the same position update handling as before (ADR 0002), so that nothing about the server's view of an Agent changes.
12. As the Human, I want milestone 3's behaviour (`stop`, a GM `/jump` mid-run, Ctrl+C, an unseen speaker) to be unchanged, so that 3b breaks nothing.
13. As the Human, I want a zone without a Navmesh to keep straight-line runs, so that an Agent outside region 200 still moves as in milestone 3.
14. As the Human, I want one script that builds the Detour library and region 200's Navmeshes and puts them where the server looks, so that setting up pathing is one step after installing the tools.
15. As the Human, I want the built library and Navmeshes kept out of git, so that rebuilding them never grows the repository.
16. As the Human, I want to know which BuildNav commit built the Navmeshes I'm running, so that I can rebuild exactly the same ones.
17. As the Human, I want NPCs in region 200 to use the server's own pathing once Navmeshes are installed, so that the world behaves like a real shard's.
18. As the Human, I want the Mind to print each route as it's worked out, so that I can see why the Agent went the way he did.
19. As a Mind developer, I want `moveTo` to take me to any point I remember, routing around what's in the way, so that Tactics never walk routes themselves.
20. As a Mind developer, I want `moveTo`'s target Z to need only be roughly right, so that a remembered point or another object's position works as a target.
21. As a Mind developer, I want an optional `within` on `moveTo`, so that I can get to melee range or speaking distance without working out where to stand.
22. As a Mind developer, I want `arrived` at once when the Agent is already within `within` of the target along the route, so that "go to X" from next to X needs no special case.
23. As a Mind developer, I want `stopped` with the reason `unreachable` when the route can't get to the target, so that I can tell "got there" from "got as close as I could" and stop retrying the same point.
24. As a Mind developer, I want a `routed` Percept with the route's corner points and whether it reaches the target, so that I know early when a target is out of reach.
25. As a Mind developer, I want `moved`, `arrived` and `stopped` to work as in milestone 3 for routed runs, so that Self keeps working unchanged.
26. As a Mind developer, I want the TypeScript types for `within`, `routed` and `unreachable` generated from the C# definitions, so that both sides always agree.
27. As a Mind developer, I want Come to send the speaker's position with `within: 100` and turn on `arrived`, so that it no longer works out a stand-off point or Z itself.
28. As the Human, I want to raise the say range to 6000 on my local server during the acceptance test and put it back afterwards, so that I can test from far away without changing the say range for good.

## Implementation Decisions

- **Building the native parts.**
  - The Human installs VS 2022 Build Tools (C++) and CMake with winget, and clones OpenDAoC-BuildNav next to this repo, as its README requires.
  - A script in this repo builds the Detour library from the repo's Detour source, builds and runs OpenDAoC-BuildNav for region 200 against the game client, and copies the library and the zone Navmeshes to where the server looks for them, in both the Debug and Release outputs.
  - It writes the BuildNav commit next to the Navmeshes. The library and the Navmeshes are gitignored.
- **Routing in the Harness (ADR 0007).** `moveTo` keeps its X, Y and Z, and gains an optional `within` in game units.
  - The target is moved to the nearest walkable point on the zone's Navmesh, and the route starts from the nearest walkable point to the Character.
  - The route is worked out with the server's existing pathfinding interface, using only walkable polygons: never doors (open or closed), water, jump links or disabled polygons. Excluding doors whatever their state keeps the query independent of live door state (ADR 0006).
  - A route that can't reach the target, or that's too long for the route buffer, is followed to its end, and the run then ends `stopped` with the new reason `unreachable`. No walkable point near the Character or the target ends it `stopped` `unreachable` at once.
  - `within` trims that many units off the end of the route. A route no longer than `within` ends the run `arrived` at once, where the Character stands. `within` doesn't apply to a route that stops short of the target.
  - Navmeshes are per zone. A target in another zone of the same region is routed to a crossing point on the zone border, as the server's NPC pathing does, and the route is worked out again in the next zone. Each new route sends a new `routed`.
  - A zone without a Navmesh (the pathfinding interface reports it unavailable) runs a straight line with interpolated Z, exactly as in milestone 3. `within` still applies, along the straight line.
- **Stepping a routed run.**
  - The run goes corner to corner at the same speed profile as milestone 3: it speeds up from standing, holds the maximum speed through corners, and slows to stop at the end.
  - Only X and Y count towards distance and speed, as in the speed check.
  - The heading points along the current leg. Z at each position update is the Navmesh floor under that X and Y, falling back to interpolating between corners if the floor query finds nothing.
  - Every position still follows from the time since the run started, so a stall never carries the Character past the end. Position updates, their rate and their speed fields, jumps, maximum speed changes, `stop` and replacing a run are all unchanged.
- **Percepts.**
  - `routed` gives the route's corner points (global), including the start and the end, and whether it reaches the target. It's sent when a route is worked out, before the first `moved` of that route.
  - `StopReason` gains `unreachable`.
  - `moved`, `arrived` and `stopped` are unchanged.
- **Codegen.** The new field, Percept and reason are C# types in the Harness area, and the generated TypeScript is regenerated, as in milestones 2 and 3.
- **Mind project.**
  - **Come.** A `come` from a known speaker sends `moveTo` to the speaker's position with `within: 100`. Its own stand-off point, Z interpolation and "already close, just turn" check go away. On `arrived` after a `come` it sends `face` towards the speaker, as before. `stop` is unchanged, and Come still listens to say lines only.
  - **Self and Surroundings.** No changes beyond the regenerated types. Self shows `stopped` with `unreachable` like any other reason, and nothing keeps `routed`.
  - **Entry point.** It prints every Percept, `routed` included.
- **Say range for testing.** For the acceptance test only, the Human raises the server's say distance to 6000 as a local, uncommitted edit, and puts it back to 512 afterwards. It's a core constant, so it's never committed.
- **Code placement.** All new server code lives in the Harness's folder and namespace, and the build script and its notes sit with the existing Detour source. No core code changes. NPCs in region 200 start using the server's existing pathing because the Navmeshes are installed, not because of any code change.

## Testing Decisions

- **Good tests** check behaviour seen from outside: given a `moveTo`, the routes and floors the Navmesh gives, and the game time, which position updates and Percepts come out. They don't check how a run is built inside.
- **Primary seam: the Harness's movement tests (existing).** The test supplies a fake of the server's existing pathfinding interface that returns set routes, closest points and floor heights, and records the filters it's asked with. Cases:
  - Following a route of several corners: the headings along each leg, and arriving at the end.
  - Z on each position update from the floor, and the fallback between corners when the floor query finds nothing.
  - An unwalkable target moved to the nearest walkable point.
  - `within` trimming the route, and `arrived` at once when the route is no longer than `within`.
  - A route that stops short ending `stopped` `unreachable` at its end. No walkable point ending it at once.
  - `routed` sent before the first `moved`, with the corners and whether it reaches the target.
  - A target in another zone: routed to the border, then a second `routed` in the next zone.
  - Filters that never include doors, water or jump links.
  - A zone with no Navmesh giving milestone 3's straight run.
  - Speed and timing across corners: no overshoot after a stall, and `stop`, jump and maximum speed changes mid-route.
  - Prior art: the existing movement and straight-run tests and the world doubles.
- **Mind (`bun test`, existing seams).** Come: a known speaker's `come` gives a `moveTo` to their position with `within: 100`, even when they're close, `arrived` gives a `face`, and an unknown speaker gives nothing. Self: `stopped` with `unreachable`.
- **Protocol codegen (existing test):** fails until the generated TypeScript is regenerated.
- **Not tested automatically:** the real Detour library and Navmeshes, which aren't in git, and the live server. They're covered by the manual acceptance test.
- **Manual acceptance test** in region 200, with the say range raised to 6000, at spots the Human picks in game:
  1. `come` from the top of a hill: he runs up it on the ground, with no floating or sinking, and the Mind prints `arrived`.
  2. `come` from behind a building or wall: he runs around it and stops about 100 units from you.
  3. `come` from across a river with a bridge or ford: he uses the crossing.
  4. `come` while you stand in deep water or on a roof: he runs as close as the ground allows, and the Mind prints `stopped` with `unreachable`.
  5. Milestone 3's checks still pass: `stop`, a GM `/jump` mid-run, Ctrl+C, a `come` from someone he can't see, and no speed-check entries in the server log.

  Afterwards, put the say range back to 512.

## Out of Scope

- Hibernia's other regions, and the other realms.
- Swimming, falling and fall damage, jumping, climbing and ladders.
- Routing through doors, and doors as typed Percepts.
- A path query the Mind calls without moving (ADR 0007's considered option).
- Region changes, ports and zone points that cross regions.
- Following a moving target and anything that steers continuously (milestone 4).
- Raising the say range for good, and listening to private messages in Come.
- Checking the built Navmeshes into git.

## Further Notes

- Vocabulary follows the root glossary, which adds **Navmesh**.
- Relevant decisions:
  - ADR 0002: Primitives go through the real packet handlers.
  - ADR 0004: results report dispatch only, so arrival, `unreachable` and the route come as Percepts.
  - ADR 0006: static world knowledge is fair. Its consequences now say routes never go through doors, because the server's Navmesh carries live door state.
  - ADR 0007 (new): `moveTo` routes along the Navmesh in the Harness.
- Facts this spec relies on, from the server as it stands:
  - The pathfinding manager falls back to a pass-through when the native library is missing.
  - With the library but no Navmesh for a zone, queries in that zone report the Navmesh unavailable.
  - Navmeshes load per zone at startup from the server's working folder.
  - Node flags are Walk, Swim, Door, Jump and Disabled, plus a runtime-only BlockingDoor that the server swaps with Door as doors open and close.
  - NPC pathing crosses zone borders by routing to an exit point and working the route out again in the next zone.
  - There's no terrain or heightmap reader. Ground height comes only from the Navmesh floor query.
  - The game client has no navmeshes, only the zone data BuildNav reads.
  - Loading every zone's Navmesh needs about 5 GB of RAM; region 200 alone is far less.
- Region 200 is Hibernia's home region: Lough Derg, Silvermine Mts., Shannon Estuary, Cliffs of Moher, Lough Gur, Bog of Cullen, Valley of Bri Leith, Connacht, Cursed Forest, Mount Collory, Cruachan Gorge, Breifine, Emain Macha and Sheeroe Hills (zones 200–208, 210–212, 214, 216). Milestone 3's acceptance test ran in Shannon Estuary (202).
- Risk: BuildNav's default agent settings (radius, step height, slope) may make routes hug walls or refuse slopes a Human can run up. If the acceptance test shows it, tune them and rebuild before touching Harness code.
- Risk: the speed check measures straight-line X and Y between updates. Sharp corners shorten that distance, so they can't trigger it, but it's worth watching the log in check 5.
- Milestone ladder for context: 1 login and logout (done) → 2 Mind connection (done) → 3 movement (done) → 3b terrain and pathing (this spec) → 4 hand-written grind Tactic → 5 LLM Planning → 6 Reflection and Tactic Library → 7 Agent character creation → 8 multiple Agents and grouping.
