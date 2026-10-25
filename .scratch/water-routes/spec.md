# Water routes: swim when it's faster

Status: ready-for-agent

## Problem Statement

Milestone 3b's routes never cross water. That kept the Agent out of rivers it couldn't swim in, but Dalta, playing for level 50, hits a wall at every lake. Every `runTo` to the east half of Lough Derg, where the dergan enchanters stand 3500 units off, ends `unreachable` on the water line. So does every run into the water east of Howth in Silvermine Mts., the same water at the same height, where the large eirebugs, cluricaun trips and sheevra are. A Human would swim across or walk round. The Mind can't do either. It can only send `moveTo`, which runs along the Navmesh, and the routes there leave the water out. Dalta's workaround is to hunt elsewhere, which costs it the best camps for its level.

## Solution

`moveTo` may now take a route through water, on the surface, whenever that's faster than going round over land. The Navmesh already holds the water as Swim polygons; routes left them out only by their filter. For each run the Harness works out two routes: one over land only, as now, and one that may swim. It times both, with Swim legs at swim speed, and runs the faster.

On a Swim leg the Agent swims the way a real 1.127 client swims, from three captures. Its position updates carry the swimming state flag and the swim speed, and Z sits 40 units below the water's surface. It never dives. The `routed` Percept marks which corners start a Swim leg, so the Mind and its Journal show where a run went into the water.

Nothing changes for the Mind's Tactics: `runTo` to a point across or in a lake just gets there.

## User Stories

1. As Dalta's Mind, I want `runTo` a point across a lake to swim across when that's faster than walking round, so that I reach the far shore in the least time.
2. As Dalta's Mind, I want `runTo` a point across a lake to walk round when that's faster than swimming, so that I never swim when running is quicker.
3. As Dalta's Mind, I want `runTo` the dergan enchanters on east Lough Derg to arrive, so that I can hunt them.
4. As Dalta's Mind, I want `runTo` into the water east of Howth to arrive, so that I can hunt the large eirebugs, cluricaun trips and sheevra there.
5. As Dalta's Mind, I want a run to a point in the water to end there, swimming, so that I can get to a Mob standing in a lake.
6. As Dalta's Mind, I want `unreachable` to mean the Navmesh can't get me there at all, even by swimming, so that I stop retrying a point that is truly out of reach.
7. As Dalta's Mind, I want `within` to work the same on a route that swims, so that I stop in melee range of a Mob in the water.
8. As Dalta's Mind, I want `routed` to say which corners start a Swim leg, so that I can tell from the route why I went into the water.
9. As Dalta's Mind, I want `moved`, `arrived` and `stopped` unchanged, so that Self and every Tactic keep working without change.
10. As Dalta's Mind, I want a speed buff to make me swim faster too, as it does a Human, so that a buffed route is timed right.
11. As Dalta's Mind, I want a route that crosses a zone border through water, as Lough Derg does into Silvermine Mts., to work like any other cross-zone route, so that the zone border is no dead end.
12. As the Human, I want a swimming Agent to look like a swimming Human in my client, at the surface and at swim speed, so that Agents don't stand out.
13. As the Human, I want a swimming Agent's position updates to match a real 1.127 client's from the captures, so that the server can't tell them apart.
14. As the Human, I want the Agent to set the swimming flag the moment it goes into the water and clear it the moment it comes out, as a client does, so that the server's view of it is right.
15. As the Human, I want an Agent that stops in the water to keep the swimming flag while it stands there, as a client does.
16. As the Human, I want the Agent's speed to drop to swim speed on going in and to ramp back up to running speed on coming out, as in the captures.
17. As the Human, I want the Agent never to set the diving flag, so that the server never bumps it up and jumps it as it does a diver where diving is off.
18. As the Human, I want a swimming Agent to pass the server's speed check, so that Agents still provably move under the same rules as Humans.
19. As the Human, I want swimming position updates to go through the same handling as running ones (ADR 0002), so that nothing about the server's view of an Agent changes but the flag.
20. As the Human, I want routes that never swim to be exactly as before, so that this work breaks nothing on land.
21. As the Human, I want a zone without a Navmesh to keep straight-line runs, so that an Agent outside region 200 moves as before.
22. As the Human, I want NPC pathing untouched, so that only the Agent's routes change.
23. As the Human, I want no change to the native Detour library or the Navmeshes, so that nothing needs rebuilding.
24. As the Human, I want the swim captures checked in with what they show, so that swimming is built from what a client sends.
25. As a Mind developer, I want the TypeScript for `routed`'s new field generated from the C# definitions, so that both sides always agree.
26. As a Mind developer, I want the Journal to record `routed` with its swim markers as it records every Percept, so that I can read back where a run swam.
27. As Dalta, I want to close my two water papercuts once runs reach both places, so that the papercuts list stays true.

## Implementation Decisions

- **Choosing the route (ADR 0007, amended).** `moveTo`'s Primitive is unchanged: X, Y, Z and an optional `within`.
  - The Router works out two routes from the same start and target, both across zone borders as now. A land route uses today's filters. A swim route uses the same filters with Swim allowed: Walk, Door and Swim included; blocking doors, jump links and disabled polygons excluded.
  - Each route gets a time: the X/Y length of its land legs at the maximum speed, plus the length of its Swim legs at swim speed. The faster wins, by any margin. A tie goes to land.
  - A route that reaches the target always beats one that doesn't. If neither reaches it, the one whose end is nearer the target wins, and the run ends `unreachable` there, as today.
  - Nearest walkable points, for the start and the target, may land on Swim polygons. So a target in the water counts as reached, and only a point more than 128 units from any walkable or swimmable point is `unreachable`.
  - No Detour area costs: the native wrapper takes include and exclude flags only (ADR 0007's considered options). Known gap: a mostly-land route with a short swim that neither query finds is missed.
- **Swim legs.** A leg is a Swim leg when the corner it starts from is on a Swim polygon, from the per-corner polygon flags the pathfinding interface already returns.
- **Swim speed.** Swim speed is 85/191 of the current maximum speed. It's the same fraction unbuffed (85 at 191) and with a speed buff (124 at 280). It's applied to the maximum speed the server last told the client, so a maximum speed change mid-run changes it too. The `WaterSpeed` bonus in the maximum-speed Percept is ignored for now.
- **Stepping a Swim leg** (the route run).
  - Z is the Navmesh floor under the position minus 24, i.e. 40 below the water's surface, since the Navmesh's water sheet sits 16 below it. The floor query on a Swim leg allows Swim polygons. If it finds nothing, Z falls back to interpolating between corners, as on land.
  - Speed on going in drops straight to swim speed, with no ramp down, as in the captures. Coming out ramps up to the maximum speed with the existing acceleration. Stopping, and starting again from standing in the water, use the existing acceleration and deceleration towards swim speed.
  - Each step says whether the Character is swimming, which drives the state flag.
  - Every position still follows from the time since the run started, so a stall never carries the Character past the end.
- **Position updates.**
  - State flag 0x04 (`SWIMMING`) is set on every position update while on a Swim leg, and on idle updates while standing where a run ended swimming. It's cleared on the first update back on land.
  - The diving action flag (0x02) is never set.
  - Everything else (rate, speed field, heading and its 0x1000 flag, action flags, health) is as when running. The speed field carries the swim speed.
  - Updates go through the same queue and handlers as before (ADR 0002). The speed check measures X/Y against the maximum speed, so swimming slower always passes it.
- **Percepts.**
  - Each `routed` corner gains `swim`: true when the leg from that corner on is a Swim leg. The last corner's `swim` says whether the route ends in the water.
  - `moved`, `arrived`, `stopped` and their reasons are unchanged. Self keeps nothing new.
- **Codegen.** The new field is in the C# Percept types in the Harness area, and the generated TypeScript is regenerated, as before.
- **Fixtures.** The three swim captures and the README's Swimming section are already checked in, and are the source for the swim position update.
- **Code placement.** All server changes live in the Harness's folder and namespace: the Router, the route run and the movement loop. No core, native or Navmesh changes.
- **Papercuts.** Once the acceptance test passes, Dalta's two water papercuts are closed with a pointer to this spec.

## Testing Decisions

- **Good tests** check behaviour seen from outside: given the routes, corner flags and floors the fake pathfinding returns, and the game time, which route is run, which position updates and Percepts come out. They don't check how the Router or the run is built inside.
- **Router (the existing routing tests and fake pathfinding):**
  - A shorter swim route beats a longer land route when its time is shorter, and loses when swimming it takes longer.
  - A tie goes to land.
  - A route that reaches the target beats one that doesn't, whichever is faster.
  - A target in the water is reached via a swim route.
  - A route crossing a zone border through water.
  - The filters asked for: today's for the land route, with Swim allowed for the swim route, and never jump links, blocking doors or disabled polygons.
  - Corner `swim` markers from the polygon flags.
  - A zone without a Navmesh still runs straight.
  - Prior art: the existing routing tests.
- **Route run (the existing route run tests):**
  - On a Swim leg: the speed is 85/191 of the maximum speed, Z is the floor minus 24, and the step says swimming.
  - Going in drops straight to swim speed. Coming out ramps back up.
  - Stopping in the water stays swimming. A maximum speed change mid-leg changes the swim speed.
  - No overshoot after a stall.
  - Prior art: the existing route run tests.
- **Harness movement (the existing movement tests):**
  - Position updates carry state flag 0x04 on Swim legs, and on idle updates after a run that ended in the water.
  - They clear it back on land. The diving flag is never set.
  - `routed` carries the `swim` markers.
  - Prior art: the existing movement tests and world doubles.
- **Golden bytes (the existing capture fixture tests):** a swimming position update matches one from `swim-straight.tsv`, masking what varies.
- **Protocol codegen (the existing test):** fails until the generated TypeScript is regenerated.
- **Mind (`bun test`):** Self treats a `routed` with `swim` markers as before. No new behaviour to test there.
- **Not tested automatically:** the real Detour library and Navmeshes, and the live server. They're covered by the manual acceptance test:
  1. Dalta `runTo` the dergan enchanters on east Lough Derg (around X:358135 Y:537884): it swims where that's faster, the Human sees it swimming at the surface, and it arrives.
  2. Dalta `runTo` into the water east of Howth (X:349000–358000 Y:590000–607000): it arrives.
  3. A `runTo` across a narrow river next to a bridge: it takes whichever is faster, and the printed `routed` shows `swim` where it swam.
  4. A run that stops in the water: Dalta stays swimming there in the Human's client.
  5. A run that never touches water behaves as before.
  6. The server log has no speed-check entries for Dalta.

## Out of Scope

- Fighting in water, and resting or sitting in water.
- Diving, breath and drowning.
- The `WaterSpeed` bonus (Mastery of Water, water breathing spells).
- Detour area costs and rebuilding the native library or the Navmeshes.
- Finding the true fastest mixed route that neither query finds.
- Water outside region 200, and the server's own water level and diving data for zones (both 0 in region 200).
- Boats and `/boat`.
- Changes to NPC pathing.

## Further Notes

- Vocabulary follows the root glossary, which now has **Swim leg** and a **Navmesh** that covers swimming.
- Relevant decisions:
  - ADR 0002: Primitives go through the real packet handlers.
  - ADR 0004: results report dispatch only, so routes come as Percepts.
  - ADR 0006: static world knowledge is fair. The water is in the Navmesh, built from the client's zone data.
  - ADR 0007 (amended): `moveTo` routes on the Navmesh in the Harness. It now records water routing as the faster of two routes, and Detour area costs as the rejected option.
- Facts this spec relies on, from the server and the tools as they stand:
  - OpenDAoC-BuildNav marks the client's water volumes as Swim polygons, a flat sheet 16 units below each water surface.
  - The pathfinding interface returns each corner with its polygon's flags.
  - NPC pathing already allows Swim polygons.
  - Lough Derg's water and the water east of Howth in Silvermine Mts. are one body at height 4613, crossing the border of zones 200 and 201.
  - On 1.127 the server stores the state flags but never sets `IsSwimming`. Its water level for region 200 is 0, so it treats nothing there as underwater.
  - The speed check doesn't depend on swimming.
- Swim behaviour comes from three captures in a river in zone 207, unbuffed (see the fixtures README's Swimming section). One earlier capture with a speed buff gave the 124-at-280 data point; it isn't checked in.
- Risk: Z = floor − 24 assumes BuildNav's sheet is 16 below the surface everywhere, which holds for its height map but not for object geometry poking through the water. If the Human sees the Agent float or sink in a spot, check the object geometry there first.
- Risk: the swimming flag follows Swim polygons, while a client sets it from depth. At a shallow shore the two may switch a step apart. It isn't checked by the server, but watch for it in acceptance check 1.
- Risk: water cells outside BuildNav's river bank polygons are Walk polygons at the sheet's height, so a route could "walk" there at running speed. If the acceptance test shows it, fix it in BuildNav's input, not in the Harness.
- Papercuts this resolves: "Routes stop at X≈347100 east of Howth" and "Routes stop at the shore of Lough Derg" in Dalta's papercuts.
