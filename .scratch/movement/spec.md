# Milestone 3: Movement

Status: ready-for-agent

## Problem Statement

Milestone 2 gave an Agent a Mind that hears what a client hears and can talk back, but its Character still can't take a step. A real client owns its own position and tells the server where it is (ADR 0002), so nothing in the server will move an Agent for it. The Mind also knows nothing about where anyone is: every server call except chat reaches it as a bare Percept naming the call. Before a grind Tactic (milestone 4) can walk to a mob, I need an Agent that can run somewhere under exactly the rules a Human's client runs under, knows where it is and where others are, and is told when it has got there or why it stopped.

## Solution

The Harness learns to run the way a real 1.127 client does. The Mind can send three new Primitives: run in a straight line to a point, stop, and face a direction. The Harness steps the Character towards the point at the speed the server allows and sends the position updates a running client sends, so the server's own movement checks apply unchanged. It tells the Mind where its Character is while it runs, when it has arrived and why it stopped, because a real client knows that too.

The server calls that tell a client about other objects become typed Percepts: NPCs and players appearing, moving and disappearing, and the server setting the Agent's own position and speed. Other players' movement, which the server sends as ready-built packets, is decoded for the one 1.127 layout that carries it (ADR 0001).

The Mind keeps two new pictures built only from Percepts: **Self**, what the Agent believes about its own Character, and **Surroundings**, what it believes is around it. The echo Mind is replaced by a Come Mind: when someone next to the Agent says `come`, it runs to them and faces them; `stop` stops it.

Terrain and pathing are not part of this milestone. The server has no navmeshes or heightmaps installed, so runs go in a straight line and Z is interpolated between start and target. That looks right on flat ground and floats or sinks over hills. A new milestone 3b adds terrain and pathing.

## User Stories

1. As the Human, I want to say `come` next to an Agent and have its Character run to me, so that I can see in game that the Mind can move it.
2. As the Human, I want the Agent to stop a little short of me rather than inside me, so that it stands where a Human would.
3. As the Human, I want the Agent to turn to face me when it arrives, so that it looks like it came to me on purpose.
4. As the Human, I want to say `stop` and have the Agent stop where it is, so that I can interrupt a run.
5. As the Human, I want a `come` from someone the Agent can't see to be ignored, so that it never runs towards a position it couldn't know.
6. As the Human, I want the Agent to run to where I stood when I said `come`, not to chase me, so that following stays a Tactic for later milestones.
7. As the Human, I want other chat, including `come` inside a longer line, to be ignored, so that the Agent only reacts to the words it's meant to.
8. As the Human, I want the Agent's run to look like a Human's run in my client, with the running animation and no jumps, so that Agents don't stand out.
9. As the Human, I want the Agent to run at the speed the server last told its client, so that it's never faster than a Human with the same Character.
10. As the Human, I want the server's speed check never to trigger on an Agent, so that Agents provably move under the same rules as Humans.
11. As the Human, I want Agent movement to go through the server's real position update handling, so that everything the server does on a Human's move (standing up, area checks, zone messages) happens for Agents too (ADR 0002).
12. As the Human, I want the Agent's position updates while running to match what a real 1.127 client sends while running, from a capture, so that the server can't tell them apart.
13. As the Human, I want the Agent's heading changes to be sent as the heading update a real client sends, so that turning on the spot looks the same.
14. As the Human, I want the Agent to keep sending idle position updates as before when it stands still, so that milestone 2's liveness is unchanged.
15. As the Human, I want an Agent crossing into another zone of the same region to say which zone it's in, as a real client does, so that the server never sends it back to the character screen and the zone messages are right.
16. As the Human, I want a run to end when the server moves the Agent itself, for example by `/jump` or by pulling it back, so that the Agent never fights the server over its position.
17. As the Human, I want the Agent to take the position the server gives it, as a real client snaps to it, so that the server stays the authority.
18. As the Human, I want Ctrl+C on a running Agent to stop it and then quit through the normal path, so that a quit isn't refused because the Character is moving.
19. As the Human, I want `quit` to stay exactly the typed `/quit`, refusal included, so that Primitives remain what a client sends.
20. As a Mind developer, I want a `moveTo` Primitive taking a target X, Y and Z, so that Mind code can send the Agent to a point.
21. As a Mind developer, I want a new `moveTo` to replace the one in progress, so that changing my mind mid-run needs no `stop` first.
22. As a Mind developer, I want a `stop` Primitive, so that I can halt a run.
23. As a Mind developer, I want a `face` Primitive taking a heading, so that I can turn the Agent without moving it.
24. As a Mind developer, I want `moveTo`, `stop` and `face` results to say only whether they were queued, so that they follow ADR 0004 like every other Primitive.
25. As a Mind developer, I want a `moved` Percept while the Agent runs, with its position, heading and speed, so that I know where it is mid-run, as its client does.
26. As a Mind developer, I want an `arrived` Percept when a `moveTo` reaches its target, carrying where the Agent ended up, so that I never have to guess arrival from snapshots.
27. As a Mind developer, I want a `stopped` Percept when a run ends without arriving, carrying where the Agent ended up and why (stopped, replaced by a new `moveTo`, moved by the server, or unable to move), so that I can tell an interruption from arrival.
28. As a Mind developer, I want a Percept when an NPC comes into view, with exactly what the 1.127 packet carries (object ID, name, position, heading, speed, level, model and so on), so that I know what's around the Agent.
29. As a Mind developer, I want a Percept when another player comes into view, with exactly what the packet carries, so that I can find the Human by name.
30. As a Mind developer, I want a Percept for each object update, with position, heading, speed, target and health, so that I can follow NPCs as they move.
31. As a Mind developer, I want a Percept for each movement of another player, decoded from the ready-built 1.127 position packet, so that I know where players are even though the server never sends it as a typed call.
32. As a Mind developer, I want a Percept when an object is removed from view, so that I can forget it.
33. As a Mind developer, I want Percepts when the server sets the Agent's own position (at world entry and on a jump) and its maximum speed, so that I know what the server decided about my Character.
34. As a Mind developer, I want every position in every Percept in global coordinates, so that I never convert between zone-relative and global myself.
35. As a Mind developer, I want any other ready-built packet to arrive as a bare Percept naming its packet code, so that I know what kind of packet came even if it isn't decoded.
36. As a Mind developer, I want TypeScript types for every new Primitive and Percept generated from their C# definitions, as in milestone 2, so that both sides always agree.
37. As a Mind developer, I want a Self module that holds the Agent's own position, heading, maximum speed and a single movement status (standing, moving to a target, arrived, stopped with a reason), built only from Percepts, so that there's one answer to "where am I and am I there yet".
38. As a Mind developer, I want a Surroundings module that holds every object the Agent knows of, with its name, position, heading and when it was last heard from, built only from Percepts, so that Mind code can find who's near.
39. As a Mind developer, I want Surroundings to forget objects more than the visibility distance from Self, checked whenever Self moves, so that it forgets what the client forgets and holds no ghosts.
40. As a Mind developer, I want Surroundings never to forget by time, so that a player standing still, whom the server doesn't send again, isn't dropped.
41. As a Mind developer, I want distances worked out when I ask for them rather than stored, so that they're never stale after Self moves.
42. As a Mind developer, I want Self and Surroundings to know nothing about Come, so that milestone 4's Tactics can reuse them unchanged.
43. As a Mind developer, I want the Come logic separate from the connection, Self and Surroundings, so that it's the only part that goes away when a smarter Mind replaces it.
44. As the Human, I want `bun mind/agent.ts <char>` to start the Come Mind, and the echo Mind and its tests to be gone, so that there's one Mind to maintain.
45. As the Human, I want the Mind to keep printing every Percept, so that I can watch what the Agent is told while it runs.
46. As the Human, I want the milestone 1 trace to keep recording every call, so that I keep the backlog for later projections.
47. As the Human, I want a real-client capture of running, turning, stopping and running past an NPC checked in next to the existing fixtures, so that movement is built from what a client sends rather than from handler code.
48. As the Human, I want all new server code to stay in the Harness's area, so that merging upstream OpenDAoC stays cheap.

## Implementation Decisions

- **Capture first.** The Human records, with `/agent capture`, a real 1.127 client running in a straight line, turning on the spot, stopping, and running past an NPC. It's checked in next to milestone 1's fixtures, and their README gets a Movement section: which fields of the position update change while running (speed, Z speed, flags), how often a running client sends position updates, what a heading update (0xBA) holds, and what a stop looks like. The movement packet builders and the update rate while running come from this capture, not from reading handler code.
- **Primitives.**
  - `moveTo(x, y, z)`: run in a straight line to the point, in global coordinates. All three are required. Running only, at the maximum speed the server last told the client. A new `moveTo` replaces the current one.
  - `stop`: end the current run where the Character is.
  - `face(heading)`: turn on the spot, sent as the heading update a real client sends. It doesn't end a run's target: a `face` during a run is immediately overtaken by the run's own heading.
  - Results report only whether they were queued (ADR 0004). Like every Primitive, they're refused while the Agent isn't in the world.
- **Movement in the Harness.**
  - A pure, tested class (like milestone 1's liveness) steps a run: given the start, target, speed and elapsed game time, it gives the position, heading and Z to send, and whether the target is reached. It never overshoots the target after a stall in the game loop.
  - Z goes linearly from the start Z to the target Z. The server never checks Z.
  - The heading points at the target throughout the run.
  - The zone ID in each position update is the zone at the current X and Y in the Character's region, from the region's static zone layout (ADR 0006).
  - While running, the Harness sends position updates at the running rate from the capture, with the speed fields a running client sets. When standing, it falls back to milestone 2's idle liveness unchanged.
  - Position updates go through the same queue and processing point as every other Harness packet (ADR 0002), so the server's speed check, soft link death, sit handling and area checks apply unchanged.
  - When the server sets the Character's position itself (a jump: `/jump`, a pull back by the speed check), the current run ends and the Harness takes the server's position, heading and zone. A maximum speed of 0 (for example while rooted) ends the current run too.
  - All of this runs only while a Mind is connected, as liveness does (ADR 0005).
- **Percepts from server calls.** Each mirrors the 1.127 packet library override that applies, gives exactly the fields that packet carries, and names the method it mirrors.
  - NPC create, player create, object update, object delete.
  - The Agent's own position at world entry, a jump, and a maximum speed update.
  - All positions are global. The object update's zone-relative coordinates are converted using the zone's static offsets (ADR 0006).
  - Doors, items on the ground, houses and region changes stay bare Percepts.
- **Ready-built packets.** Some server code builds a packet itself and hands it to the packet library's raw send calls (ADR 0001). The 1.127 player position packet that the server broadcasts when another player moves is decoded into a typed Percept, mirroring the server code that builds it. Every other raw packet becomes a bare Percept naming its packet code. This is the only layout decoded; there's no general byte parser.
- **Percepts the Harness works out itself.** These are what the Agent's client knows because it computed it, not because the server said so.
  - `moved`: sent with each position update while running. Position, heading and speed.
  - `arrived`: the target was reached. The position where the Character ended up.
  - `stopped`: the run ended without arriving. The position where the Character ended up and the reason: `stop`, replaced by a new `moveTo`, moved by the server, or unable to move.
  - They go into the same Percept stream, in order with the server-call Percepts.
- **Codegen.** The new Primitives, Percepts and the reason enum are C# types in the Harness area, and the generated TypeScript is regenerated, as in milestone 2.
- **Mind project.**
  - **Self module.** Fed Percepts, it keeps the Agent's own position, heading, maximum speed, and a single movement status: standing, moving to a target, arrived, or stopped with a reason. The status comes only from `moved`, `arrived`, `stopped` and the server's own-position Percepts. It doesn't track Primitives it sent. It uses the type name `Self` and never a variable named `self`, which is a JavaScript global.
  - **Surroundings module.** Fed Percepts, it keeps every known object by object ID: name, position, heading, speed and when it was last heard from. Created by create Percepts, updated by object update and player position Percepts, removed by delete Percepts. Whenever Self's position changes, including a server jump, it forgets objects farther than the visibility distance (6000) from Self. It never forgets by time. It answers "who is called X" and "how far is this from Self", working distances out when asked.
  - **Come module.** Given a Percept, Self and Surroundings, it returns the Primitives to send, or nothing:
    - A say line from someone else whose whole text is `come` (case-insensitive) gives a `moveTo` to the point 100 units short of the speaker's position in Surroundings, along the line from Self. A speaker not in Surroundings gives nothing. A speaker already within 100 units gives a `face` only.
    - A say line `stop` from someone else gives a `stop`.
    - On `arrived` after a `come`, it gives a `face` towards that speaker's last known position.
    - Anything else gives nothing. The speaker comes from the say line's text, as in the echo Mind.
  - **Connection module.** Ctrl+C now sends `stop` and then `quit`. Otherwise unchanged.
  - **Entry point.** Starts the Come Mind and prints every Percept. The echo module and its tests are deleted.
- **Code placement.** All new server code lives in the Harness's folder and namespace. No core code changes.

## Testing Decisions

- **Good tests** check behaviour seen from outside: which Percept comes out of a call or a raw packet, which bytes a Primitive produces, where a run puts the Character after a given time, what the Mind's modules hold and which Primitives the Come logic returns. They don't check how anything is built inside.
- **Primary seam: the Come Mind against a running server, by hand.** This is milestone 3's acceptance test:
  1. Start the Mind. Greeen logs in.
  2. Stand about 30 m away on flat ground and `/say come`. He runs to about 100 units short of you, smoothly in your client (running animation, no jumps), and turns to face you. The Mind prints `arrived`.
  3. `/say come`, then `/say stop` while he runs. He stops, and the Mind prints `stopped` with the stop reason.
  4. `/say come`, then move him mid-run with a GM `/jump`. He's at the new spot, and the Mind prints `stopped` with the server-move reason.
  5. Ctrl+C during a run. He stops, then quits through the normal path.
  6. The server log has no speed-check entries for him.
  7. Someone he can't see says `come`: nothing happens.

  Hills are a known gap until milestone 3b; test on flat ground.
- **Movement stepping (server unit tests, new seam):** arriving exactly at the target, heading towards the target, Z interpolation, no overshoot after a stall, the zone ID changing at a zone border within a region, and a stop mid-run. Prior art: the liveness tests.
- **Golden bytes (existing seam):** a running position update with its speed fields, and a heading update, match the new capture, masking what varies. Prior art: the capture-fixture and 1.127 packet builder tests.
- **Percept projections (server unit tests, existing seam from milestone 2):** one test per new call: NPC create, player create, object update (with zone-relative coordinates converted to global), object delete, own position, jump, maximum speed. The player position decoder is fed the server's own 1.127 builder output and gives back what went in. A raw packet of another kind gives a bare Percept naming its code. Prior art: the message projection tests.
- **Protocol codegen (existing test):** fails until the generated TypeScript is regenerated.
- **Mind (`bun test`, new seams):**
  - Self: the movement status through `moved`, `arrived`, `stopped` and a server jump; maximum speed updates.
  - Surroundings: create, update, player position, delete; forgetting beyond the visibility distance when Self moves; never forgetting by time; lookup by name; distance from Self.
  - Come: another speaker's `come` gives the right `moveTo`; an unknown speaker gives nothing; a speaker within 100 units gives a `face`; `stop` gives `stop`; `arrived` after `come` gives a `face`; the Agent's own lines, other chat types and longer lines give nothing.
  - Connection: Ctrl+C sends `stop` before `quit`. Prior art: the existing connection tests.
- **Not tested automatically:** the live movement loop through the server's handlers and its speed check. That would need a database and a booted world, as in milestones 1 and 2. They're covered by the manual acceptance test.

## Out of Scope

- Terrain, navmeshes, ground height and pathing (milestone 3b).
- Region changes, ports and zone points that cross regions.
- Walking, backing up, strafing, swimming, jumping, falling, climbing and mounts.
- Following a moving target, `/stick`, and anything that steers continuously (Tactics, milestone 4).
- Doors, items on the ground and houses as typed Percepts.
- A `within` distance on `moveTo`; where to stand is Mind code.
- Waits on Percepts, a convenience layer over Primitives and cancellation of running Mind code (milestone 4).
- Any LLM use (milestone 5 onwards).
- More than one Agent online at once (milestone 8).

## Further Notes

- Vocabulary follows the root glossary. This milestone widens **Percept** to cover what a client works out itself, and adds **Self** and **Surroundings**.
- Relevant decisions:
  - ADR 0001: Percepts come from a custom packet library. Its consequences now cover the one decoded raw packet layout.
  - ADR 0002: Primitives go through the real packet handlers, and the Harness simulates movement itself.
  - ADR 0004: a Primitive's result reports dispatch only. Arrival and interruption come from Percepts.
  - ADR 0005: movement, like liveness, happens only while a Mind is connected.
  - ADR 0006 (new): static world knowledge (terrain, zone layout, visibility distance) is fair; dynamic knowledge comes only from Percepts.
- Background research: `.scratch/movement/research/rs-sdk-self-state.md`. rs-sdk has no movement status and works out arrival in several places with different thresholds, which led to false arrivals. That's why Self keeps one status built from the Harness's own Percepts.
- Facts this spec relies on, from the server as it stands:
  - The speed check measures X and Y only, allows 1.15 times the maximum speed, pulls the player back after six violations within 1.5 s, and disconnects after five pull-backs. Z and fall damage are taken from the client.
  - An unknown zone ID in a position update sends the client back to the character screen; a wrong but existing one isn't checked against X and Y.
  - NPCs are created and updated every 5 s within a visibility distance of 6000, and never deleted when they leave it; the client forgets them itself.
  - Other players' movement is broadcast as a ready-built packet at most every 200 ms, and only when they move.
  - `/quit` is refused while the Character is moving.
  - No navmeshes or native pathing library are installed, so the server's pathfinding passes through.
- Risk: the running update rate and speed fields come from one capture. If the server's speed check still triggers in the acceptance test, compare the Harness's updates with the capture before touching thresholds.
- Risk: straight runs go through walls, trees and water. Fine for the acceptance test on flat open ground; milestone 3b fixes it.
- Milestone ladder for context: 1 login and logout (done) → 2 Mind connection (done) → 3 movement (this spec) → 3b terrain and pathing → 4 hand-written grind Tactic → 5 LLM Planning → 6 Reflection and Tactic Library → 7 Agent character creation → 8 multiple Agents and grouping.
