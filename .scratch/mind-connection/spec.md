# Milestone 2: Mind connection

Status: resolved

## Problem Statement

Milestone 1 proved that an Agent's Character can enter and leave the world through the Harness, with no real game client. But nothing decides anything for it yet. An admin command logs it in, and then it just stands there. Before movement, Tactics or any LLM work, I need the other half of the Agent: a Mind running outside the server that is told what a client would be told and can act on it. And I need proof that the round trip works end to end, from the server to the Mind and back into the world. I also don't want Agents standing around in the world with nothing behind them.

## Solution

A Mind is a TypeScript program running on Bun, one process per Agent, living in this repo. Starting it, for example `bun mind/agent.ts greeen`, connects to the server over a local WebSocket and logs the Agent's Character in through the Harness. The Mind's connection is the Agent's network connection (ADR 0005):
- While the Mind is connected, the Harness keeps the Character alive the way a real client does.
- Ctrl+C makes the Agent quit through the normal quit path.
- If the Mind crashes, the Character goes linkdead under the server's normal rules and is gone 60 s later.

The Mind receives Percepts and can send Primitives. Each Percept carries only what the real 1.127 packet would carry. A Primitive's result says only that it was queued (ADR 0004). Milestone 2's Mind is an echo Mind with no LLM:
- When someone says something next to the Agent, the Agent says it back with a new `say` Primitive.
- The Mind prints every Percept it receives.

`/agent login` and `/agent soak` are retired; starting a Mind is now how an Agent logs in. `/agent logout` stays as the admin's kick.

## User Stories

1. As the Human, I want to start an Agent's Mind with one command naming the Agent's Character, so that the Agent logs in without any admin command.
2. As the Human, I want the Agent's Character to appear at its saved location when its Mind starts, so that I can see in game that the Mind got in.
3. As the Human, I want the Mind to tell me clearly when the Character is in the world, so that I know login completed rather than just started.
4. As the Human, I want the Mind to tell me why a login was refused and then exit, so that typos and wrong Accounts don't fail silently.
5. As the Human, I want a Mind for a Character that doesn't belong to an Agent Account to be refused, so that a Mind can never take over a Human's Character.
6. As the Human, I want a Mind for an Agent whose Account I'm logged into with the real client to be refused, so that Possession and a Mind never fight over one Character.
7. As the Human, I want my real client's login to an Agent Account to be refused while that Agent's Mind is connected, so that I can't possess an Agent that's playing.
8. As the Human, I want only programs on my own machine to be able to connect as a Mind, so that nobody on the network can drive my Agents.
9. As the Human, I want a Mind to present a shared token from the server's configuration, so that a stray local program can't log an Agent in by accident.
10. As the Human, I want Mind connections to need no per-Agent password, so that I don't have to manage Agent passwords.
11. As the Human, I want the Harness to keep a connected Agent alive with the pings and position updates a real client sends, so that the Mind never has to deal with liveness.
12. As the Human, I want an Agent whose Mind is busy but still running to stay online, so that a slow decision never looks like a dropped connection.
13. As the Human, I want Ctrl+C on a Mind to make its Agent quit through the normal quit path, quit delay included, so that its Character is saved and removed like a Human's.
14. As the Human, I want the Mind process to exit only after the server has disconnected its Agent, so that I know the quit finished.
15. As the Human, I want an Agent whose Mind crashed or stopped answering to go linkdead like a Human whose client crashed, so that Agents get no special treatment and nothing is left behind.
16. As the Human, I want a linkdead Agent to be gone after the server's normal link-death window, so that there are never Agents in the world without a Mind for long.
17. As the Human, I want a Mind started for an Agent that's still linkdead to be refused and to retry by itself, so that I can restart a crashed Mind right away and it gets in as soon as it's allowed.
18. As the Human, I want a second Mind for an Agent that's online with a live Mind to take over the connection without a logout, so that restarting a Mind whose old connection hasn't timed out yet doesn't get in my way.
19. As the Human, I want the replaced Mind to be told it was replaced and to exit, so that two Minds never act for one Agent.
20. As the Human, I want `/agent logout <char>` to quit an Agent through the normal quit path and end its Mind's connection, so that I can remove an Agent from the game myself.
21. As the Human, I want `/agent logout` on a Character that isn't online as an Agent to fail with a clear message, so that I know nothing happened.
22. As the Human, I want `/agent login` and `/agent soak` to be gone, so that there's only one way for an Agent to enter the world.
23. As the Human, I want `/agent capture` to keep working, so that I can record what a real client sends for new Primitives.
24. As the Human, I want only admins to be able to use `/agent`, so that nobody else can remove Agents.
25. As the Human, I want the Mind to receive a Percept for everything the server tells the Agent's client, so that it never misses that something happened.
26. As the Human, I want a Percept to carry only what the real 1.127 packet carries, so that an Agent knows only what a Human's client would know (ADR 0001).
27. As the Human, I want server-to-client calls that don't have a projection yet to arrive as a Percept naming the call and nothing else, so that the Mind knows something happened without being told more than a client would.
28. As the Human, I want chat and system messages to arrive as Percepts with their text, chat type and where they show, so that the Mind sees what a Human sees in their chat window.
29. As the Human, I want Percepts to arrive in the order the server made the calls, each with its time, so that the Mind can reconstruct what happened.
30. As the Human, I want the Mind to print every incoming Percept to its console, so that I can watch what the Agent is told while it plays.
31. As the Human, I want sending Percepts to the Mind never to slow down or block the game loop, so that a slow or stuck Mind can't degrade the server for everyone.
32. As the Human, I want the Mind to be able to make the Agent say something, so that the Agent can talk in game.
33. As the Human, I want the `say` Primitive to send exactly the packet a real 1.127 client sends when a Human types a say line, so that the server handles it exactly as it would a Human's.
34. As the Human, I want Primitives to be processed at the same point in the client tick as a Human's packets, so that ordering and threading match real clients (ADR 0002).
35. As the Human, I want a Primitive's result to say only whether it was queued, so that the Agent learns whether it worked from Percepts, as a Human does (ADR 0004).
36. As the Human, I want a Primitive sent before the Agent is in the world, or after it has left, to be refused with a reason, so that the Mind can't act on a Character that isn't there.
37. As the Human, I want the echo Mind to repeat what others say next to the Agent, so that I can check the whole round trip by talking to it.
38. As the Human, I want the echo Mind to ignore its own lines, so that it never echoes itself.
39. As the Human, I want the echo Mind to ignore chat other than say, so that system messages and other channels don't trigger replies.
40. As the Human, I want a malformed or unexpected message from a Mind to be refused without affecting the Agent or the server, so that a buggy Mind can't crash anything.
41. As a Mind developer, I want TypeScript types for every Primitive and Percept generated from their C# definitions, so that both sides of the connection always agree.
42. As a Mind developer, I want a test to fail when the generated TypeScript is out of date, and to say how to regenerate it, so that stale types never go unnoticed.
43. As a Mind developer, I want the Mind's code in its own top-level folder of this repo with its own Bun project, so that it builds and tests separately from the server.
44. As a Mind developer, I want the connection handling separated from the echo logic, so that later Minds can reuse the connection unchanged.
45. As the Human, I want the server's per-login trace from milestone 1 to keep recording every call, so that I still have the backlog of Percepts to project in later milestones.
46. As the Human, I want the server to handle an Agent whose Mind is still connected at shutdown the same way it handles online Humans, so that shutdown doesn't hang or lose Agent state.
47. As the Human, I want all new server code to stay in the Harness's own area, so that merging upstream OpenDAoC stays cheap.

## Implementation Decisions

- **Mind endpoint.**
  - The server hosts a WebSocket endpoint for Minds on its own small ASP.NET listener, bound to localhost only. ASP.NET is already part of the server; this listener is separate from the existing API host, which may be switched off.
  - The port and the shared token are server properties in a new Agents category. With no token configured, the endpoint refuses every Mind.
  - The listener starts and stops with the server.
- **Wire protocol.** JSON over WebSocket, one message per frame, each with a `type` discriminator.
  - Mind to server:
    - `hello`: the token and the Agent's Character name. It must be the first message.
    - `primitive`: a request ID, the Primitive's name and its arguments.
  - Server to Mind:
    - `refused`: a reason, and whether the Mind should retry. The server closes the connection after sending it.
    - `entered`: the Character is in the world.
    - `percept`: a sequence number, milliseconds since login, and the Percept.
    - `primitiveResult`: the request ID, whether the Primitive was queued, and a reason if it wasn't.
    - `replaced`: another Mind took over. The server closes the connection after sending it.
  - When the Agent's Harness client disconnects (quit, kick, link-death expiry), the server closes the WebSocket normally. A normal close after `entered` means the Agent has left the world.
  - The message shapes are part of the generated TypeScript (see Codegen).
- **Login.**
  - `hello` runs milestone 1's login checks, reused unchanged. The Character must exist and belong to an Agent Account. Its Account must not be logged in with a real client and not be online as an Agent.
  - Then the Harness client enters the world as in milestone 1, and `entered` is sent once the Character is in the world. A world entry that fails or times out is sent as `refused`.
  - The handshake runs on the game loop, as `/agent` commands from the console already do.
- **Already online.**
  - If the Agent is online and its Harness client has a live Mind connection, the new connection takes over: the old Mind gets `replaced`, and the Character stays in the world.
  - If the Agent's Harness client is linkdead (no Mind connection, link-death timer running), the new Mind is refused with retry set. The Character can log in again once the server has removed it.
- **Mind connection lifecycle (ADR 0005).**
  - The Harness client sends pings and position updates (milestone 1's liveness) only while a Mind is connected.
  - The server detects a lost Mind from the WebSocket closing or from missed WebSocket keep-alive pings. Bun answers those itself, so a busy Mind stays connected.
  - On loss, the Harness client disconnects the way a closed socket would. While Playing, the server takes that as a link death: nearby players are told, the Character stays for the link-death window, then quits and is saved.
  - Neither the Harness nor the Mind adds a grace period of its own.
- **Primitives in milestone 2.**
  - `say <text>`: the command packet a real 1.127 client sends for a say line. Its exact layout comes from a capture with the real client, not from reading handler code. (Milestone 1 found the command packet carries the typed command with `&` in place of `/`, and the captured chat line will confirm whether say goes the same way.)
  - `quit`: the real `/quit`, exactly as milestone 1's logout sends it.
  - A Primitive is accepted only while the Agent is in the world. Its result reports only whether it was queued on the Harness client's inbound queue (ADR 0004). Primitives go through the same queue and processing point as milestone 1's packets (ADR 0002).
- **Percepts.**
  - Every call the server makes on the Agent's packet library produces exactly one Percept, in call order.
  - A call with a hand-written projection produces a typed Percept. Its fields are named by meaning, and they are exactly the fields the 1.127 packet library chain writes for that call. Each projection says which packet-library method it mirrors.
  - Milestone 2 projects one call: the message call (text, chat type, chat location). Chat types and locations go over the wire as their enum names.
  - Every other call produces a bare Percept carrying only the call's name. Property reads on the packet library, which tell the client nothing, produce no Percept.
  - The projection runs on the game loop, because its arguments are live game objects. The finished message goes into a per-connection queue that never waits, drained by a sender off the game loop, as the trace does.
  - The trace keeps recording every call, unchanged.
- **Codegen.** The Primitive and Percept definitions, and the protocol messages, are C# types in the Harness area. A generator turns them into one TypeScript file in the Mind project, which is checked in.
- **`/agent` command.**
  - The `login` and `soak` subcommands and the soak runner are removed.
  - `logout <char>` quits the Agent through the real `/quit` path, as before. When the Harness client disconnects, the Mind's connection is closed.
  - `capture` is unchanged.
  - The command stays admin-only.
- **Mind project.** A Bun project in a new top-level folder, with three parts:
  - **Connection module.** It connects, sends `hello`, waits for `entered` or `refused`, and retries every few seconds while a refusal says to. It delivers Percepts to a handler, sends Primitives, and on Ctrl+C sends `quit` and waits for the server to close the connection before exiting. Exits on `replaced` or on any other refusal. It knows nothing about echoing.
  - **Echo module.** Given a Percept and the Agent's own name, it returns a `say` Primitive or nothing. It reads the speaker and the words from the say line's text (for example `Name says, "words"`), because the packet has no separate speaker field.
  - **Entry point.** It takes the Character name from the command line and the server address and token from the environment (an untracked local env file). It prints every Percept to stdout.
- **Code placement.** All new server code lives in the Harness's folder and namespace. The core seams from milestone 1 aren't widened, and no other core code changes.

## Testing Decisions

- **Good tests** check behaviour you can see from outside: which Percept comes out for a call, which bytes a Primitive produces, which reply the echo logic gives. They don't check how the Harness or the Mind is built inside.
- **Primary seam: the Mind protocol against a running server, by hand.** This is milestone 2's acceptance test:
  1. Start the Mind. Greeen appears, and the Mind reports he's in the world.
  2. `/say hello` next to him, and he says `hello` back. His own line doesn't make him say it again.
  3. Ctrl+C. He quits through the normal path, and the Mind exits after the server disconnects him.
  4. Start the Mind again, then kill its process. Nearby players see him go linkdead, and he's gone after 60 s.
  5. Start a Mind again while he's still linkdead. It reports it's retrying, and logs in once he's gone.
  6. Start a second Mind while one is connected. The first reports it was replaced and exits, and Greeen stays in the world.
  7. Log in to his Account with the real client while a Mind is connected. You're refused.
  8. `/agent logout greeen` makes him quit and the Mind exits.
  9. Start a Mind with a wrong token, and with a Character on a Human Account. Both are refused with a clear reason.

  No soak run.
- **Percept projection (server unit tests, new seam).** One packet-library call goes in, one Percept comes out:
  - The message call gives exactly its text, chat type and location.
  - A call without a projection gives a bare Percept with only its name.
  - Property reads give nothing.
  - Prior art: the plain NUnit tests of the trace (`UT_AgentTrace`).
- **Protocol codegen (server unit test).** It generates the TypeScript from the C# types and fails if the checked-in file differs, naming the command that regenerates it. This follows rs-sdk's test that keeps its generated API reference in sync.
- **Golden bytes for `say` (existing seam).**
  - The Human types one say line with the real 1.127 client while `/agent capture` runs. The capture is checked in next to milestone 1's fixtures, and its README gets a note on what it holds.
  - The test checks that the `say` builder produces the captured bytes, masking anything that varies.
  - Prior art: the capture-fixture and 1.127 packet builder tests.
- **Echo logic (Mind, `bun test`, new seam):**
  - Another speaker's say line gives a `say` with the same words.
  - The Agent's own say line gives nothing.
  - Other chat types and unparsable text give nothing.
- **Not tested automatically:** the WebSocket endpoint and login handshake end to end. That would need a database and a booted world, as in milestone 1. They're covered by the manual acceptance test.

## Out of Scope

- Any LLM use, Planning, Reflection, Tactics, Lessons or Journal (milestones 4–6).
- Primitives other than `say` and `quit`: movement, combat, emotes, other chat channels.
- Percept projections other than the message call, including object creates, updates and removes (milestone 3).
- Pathfinding and map knowledge (milestone 3).
- Waits on Percepts, built-in Tactics, a convenience layer over Primitives, and cancellation of running code (milestone 4).
- Sandboxing Mind code, marking chat as untrusted when shown to an LLM, and an `execute_code`-style entry point for an LLM (milestone 5).
- Agent feedback or bug reports and the Journal's contents (milestone 6).
- More than one Agent online at once beyond what falls out naturally. Two echo Minds next to each other would echo each other forever; that's left for milestone 8.
- A soak test, or any automated test of the full Mind connection.
- Mind connections from other machines.
- Restarting a crashed Mind automatically. Its retry only covers a login refused during link death.
- An evaluation score for comparing Minds (unscheduled).

## Further Notes

- Vocabulary follows the root glossary. Since this milestone, the Harness includes everything a client does on its own, such as keeping the connection alive, and a Primitive is something an Agent decides to do.
- Relevant decisions:
  - ADR 0001: Percepts come from a custom packet library.
  - ADR 0002: Primitives go through the real packet handlers.
  - ADR 0003: the Mind is a separate TypeScript process over WebSocket, now on Bun with no sandbox yet.
  - ADR 0004: a Primitive's result reports dispatch only.
  - ADR 0005: the Mind's connection is the Agent's network connection.
- Background research: `.scratch/mind-connection/research/rs-sdk.md`, cited notes on how rs-sdk solved the same problem for a RuneScape emulator.
- Hand-written projections are affordable. Across milestone 1's 116 traces, the server made 47 different calls, and five of them account for about 97% of all calls. Milestone 3 needs about six more projections.
- Risk: the 1.127 packet library is a chain of overrides from older versions. A projection has to mirror the override that actually applies to 1.127, not the first one found.
- Risk: three group-member calls take a span argument that the runtime proxy can't box (found in milestone 1). They can't produce Percepts until that's solved, which matters at milestone 8.
- Risk: a Mind that stops reading its socket makes the server's send queue for it grow. Milestone 1 accepted the same risk for the trace. Revisit when several Agents are online.
- Milestone ladder for context: 1 login and logout (done) → 2 Mind connection (this spec) → 3 movement → 4 hand-written grind Tactic → 5 LLM Planning → 6 Reflection and Tactic Library → 7 Agent character creation → 8 multiple Agents and grouping.

## Comments

**2026-10-20 (Human, recorded by agent):** Milestone 2 is done. All five tickets are resolved, and the manual acceptance test in Testing Decisions passed across the in-game checks of tickets 02 to 05: an echo Mind logs Greeen in, answers each say line exactly once, quits cleanly on Ctrl+C, goes linkdead when killed, is taken over by a second Mind, retries while Greeen is linkdead, and refuses everything it should. Next on the ladder is milestone 3 (movement).
