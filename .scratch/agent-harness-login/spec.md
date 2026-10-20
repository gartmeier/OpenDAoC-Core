# Milestone 1: Agent Harness login and logout

Status: resolved

## Problem Statement

I want LLM-driven Agents to play on my server so I can watch how they play differently from how Humans did 20 years ago. Earlier attempts collapsed under building a whole game client from scratch and never got an Agent into the world at all. Before any Mind, LLM, movement or combat work makes sense, I need proof that a Character can enter the world and leave it again cleanly without a real game client, going through the same server paths a Human's client uses, and with the server none the wiser.

## Solution

A Harness inside the server stands in for the game client of an Agent's Character. As an admin, I type `/agent login <char>` and the Agent's Character appears next to me in the world as if a real client had logged it in. `/agent logout <char>` makes it quit through the normal quit path and disappear cleanly. `/agent soak <char> <n>` runs n login/logout cycles and reports whether each was clean. While logged in, the Harness records a trace of everything the server tells it. That trace is the inventory of Percepts future milestones will build on.

There is no Mind and no LLM in this milestone. The Agent's Account and Character are created beforehand by the Human with the real client.

## User Stories

1. As the Human, I want to type `/agent login <char>` in game, so that an Agent's Character enters the world without a real game client.
2. As the Human, I want the Agent's Character to appear at its saved location, so that I can walk over and see it standing there with my own client.
3. As the Human, I want the Agent's Character to appear in `/who`, so that I know the server treats it like any other online Character.
4. As the Human, I want to target and inspect the Agent's Character with my own client, so that I can confirm it's a normal in-world Character.
5. As the Human, I want `/agent login` to go through the same character-select and world-entry steps a real 1.127 client does, so that the Agent's world entry is authentic and exercises the same initialisation as a Human's.
6. As the Human, I want the Harness to skip the network handshake, version check and password check, so that I don't need to reimplement encryption or store Agent passwords.
7. As the Human, I want `/agent login` to refuse a Character that's already online (by a Human possessing it or by another Harness), so that two clients never fight over one Character.
8. As the Human, I want `/agent login` to refuse an unknown Character name with a clear message, so that typos don't fail silently.
9. As the Human, I want `/agent login` to refuse a Character that doesn't belong to an Agent Account, so that I can't accidentally hijack a Human's Character.
10. As the Human, I want to see a clear success message once the Agent's Character is actually in the world, so that I know login completed rather than just started.
11. As the Human, I want a clear failure message if world entry doesn't complete within a reasonable time, so that a stuck login doesn't look like a success.
12. As the Human, I want `/agent logout <char>` to make the Agent quit through the normal quit path, so that its Character is saved and removed exactly as a Human's would be.
13. As the Human, I want the Agent's Character to vanish from my screen and from `/who` after logout, so that I can see logout completed.
14. As the Human, I want the Agent's location and state to persist across logout and login, so that I know saving works.
15. As the Human, I want the Agent's Character never to become linkdead during login, idle time or logout, so that the Harness never looks like a dropped connection to the server.
16. As the Human, I want the Harness to answer the server's liveness checks the way a real client does, so that it isn't disconnected for timing out while idle.
17. As the Human, I want a logged-out Agent to release its client slot and session, so that repeated logins don't leak server resources.
18. As the Human, I want `/agent logout` on a Character that isn't online as an Agent to fail with a clear message, so that I know nothing happened.
19. As the Human, I want the server to handle an Agent that's still online at shutdown the same way it handles online Humans, so that shutdown doesn't hang or lose Agent state.
20. As the Human, I want `/agent soak <char> <n>` to run n full login/logout cycles back to back, so that I can check reliability without typing 100 commands.
21. As the Human, I want the soak run to check that each cycle reached in-world play and then fully disconnected, so that a partial cycle counts as a failure.
22. As the Human, I want the soak run to check that the online client count and session allocation are back to their starting values afterwards, so that leaks are detected.
23. As the Human, I want the soak run to report pass or fail with the number of the first failing cycle and why, so that failures are actionable.
24. As the Human, I want the soak run to stop on the first failure, so that one broken cycle doesn't cascade into noise.
25. As the Human, I want only admins to be able to use `/agent`, so that nobody else can spawn or remove Agents.
26. As the Human, I want the Harness to record a trace of every call the server makes to tell the Agent's client something, so that I can see exactly what a client is told during world entry and while idle.
27. As the Human, I want the trace to be per Agent and to include the order and time of each call, so that I can reconstruct the world-entry sequence.
28. As the Human, I want recording the trace never to slow down or block the game loop, so that Agents can't degrade the server for everyone.
29. As a future Mind developer, I want that trace to serve as the backlog of Percepts to implement, so that perception work is driven by what the server actually sends.
30. As the Human, I want all Harness code kept in its own area with only minimal, separately committed seams in the core client code, so that merging upstream OpenDAoC stays cheap.
31. As the Human, I want the core seam changes to be small and generic (supporting a client without a socket), so that they could be proposed upstream as headless-client support.
32. As the Human, I want Harness packets to be processed at the same point in the client tick as a Human's packets, so that ordering and threading match real clients.
33. As the Human, I want every packet the Harness builds to match byte for byte what a real 1.127 client sends (apart from fields that legitimately vary), so that the handlers see exactly what they would from a Human.
34. As the Human, I want an Agent Account to be recognisable by its naming convention, so that I and the Harness can tell Agent Accounts from Human ones.
35. As the Human, I want to be able to log into an Agent Account with the real client (Possession) when the Agent is offline, so that I can create its Character and inspect it with normal tools.
36. As the Human, I want the idle-kick rules to apply to Agents exactly as they do to Humans at the same privilege level, so that Agents get no special treatment.

## Implementation Decisions

- **Headless game client.** A new Harness client type derives from the existing game client and has no socket. It overrides only the members that touch the socket:
  - Receiving drains a thread-safe queue of synthesized inbound packets and passes them through the normal packet processor.
  - Sending and closing the socket become no-ops.
  - The client registers with the client service like a real connection, including session ID allocation. It goes through the same tick states (character screen, world entry, playing, disconnected).
  - The core changes needed to allow this (making a small number of client and packet-processor members overridable, and making the pending-packet send path tolerate a missing socket) go in separate small commits (ADR 0001, ADR 0002, "upstream hygiene").
  - So that soak can compare session allocation, the server's session ID allocator also counts the IDs it holds (a read-only count, in its own commit).
- **Percepts via a custom packet library** (ADR 0001).
  - The Harness client's outbound packet library is a Harness implementation of the server's packet-library interface. Each call is appended to the Agent's trace and otherwise ignored for now.
  - It is called on the game loop thread and must never block: trace writes go through a queue drained off the game loop.
  - A few calls need a client-like reaction. The server's player-quit call makes the Harness complete disconnection the way a real client closes its connection. Ping replies are recorded.
- **Primitives via real packet handlers** (ADR 0002). Milestone 1 needs internal packet builders for one fixed client version, 1.127:
  - realm or character overview request
  - character select
  - world init request (carrying the character slot index derived from the Account's realm and the Character's account slot)
  - whatever further requests a real 1.127 client sends to complete world entry (for example player init and region list)
  - ping
  - position update, repeating the Character's current position
  - the `/quit` command packet

  The exact order and contents of the world-entry sequence come from capturing a real 1.127 client, not from reading handler code.
- **Login flow.**
  - `/agent login` loads the Account that owns the named Character.
  - It creates a Harness client with that Account and the Harness packet library already attached, so the network handshake, version check and password check are skipped.
  - It enqueues the captured sequence from character select onwards and waits for the client to reach in-world play.
  - World entry includes an asynchronous character load, so login completion is detected from client state, not assumed after enqueuing.
- **Logout flow.**
  - `/agent logout` enqueues the real `/quit` command packet (`&quit`; the captured Human typed the `&q` alias). The server's normal quit path runs, including any quit delay it applies.
  - On the resulting player-quit call, the Harness triggers the normal client disconnect, so the Character is saved and removed through the existing path. Like the real client, it first returns to the character screen (character select, overview) and closes the connection from there. The server leaves the client Playing after the quit, so closing right away would count as a link death.
- **Liveness.** While online, the Harness sends what a real idle client sends, at the real client's intervals: a ping every 5 s and a position update about every 2.1 s that repeats the Character's current position. Without position updates the server soft link-deads a player after 5 s, including during the quit delay. The Harness is subject to the normal ping timeout and link-death rules. It skips the client's UDP init and ping, because the server falls back to TCP without them. Repeating the current position isn't movement, which stays out of scope.
- **Agent Accounts** are ordinary Accounts identified by a naming convention (an `agent` prefix, e.g. `agent1`; the server rejects underscores in account names). `/agent login` refuses Characters on other Accounts, and Characters already online.
- **Registry.** An in-memory registry maps Character names to their live Harness clients, so that `/agent logout` and `/agent soak` can find them and duplicate logins can be refused. It holds no persistent state.
- **Admin command.** `/agent` requires admin privilege and has three subcommands: `login <char>`, `logout <char>`, and `soak <char> <n>`. Soak runs cycles one after another (not in parallel), stops at the first failure, and reports pass or fail, the failing cycle and the reason. It compares online client count and session allocation against their values before the run.
- **Code placement.** All Harness code lives in its own folder and namespace inside the game server project. Nothing outside the listed core seams changes. The one exception is the inbound packet capture hooks in the TCP and UDP receive paths, which are off by default and kept for recapturing fixtures (ticket 01).

## Testing Decisions

- **Good tests** check externally observable behaviour: what enters and leaves the world, client states the server exposes, bytes produced. They don't check internal structure. A refactor of Harness internals shouldn't break them.
- **Primary seam: the `/agent` admin command on a running server.** This is milestone 1's acceptance test:
  - Manual: login, check in-world visibility and `/who`, idle past a few ping intervals without linkdead or timeout, logout, check the Character is gone and its location was saved, log in again.
  - Automated in-server: `/agent soak <char> 50` passes with a clean server log.
- **Secondary seam: golden-byte unit tests for the 1.127 packet builders.** Capture the inbound packets a real 1.127 client sends from character select through world entry and for `/quit` once, and check them in as fixtures. Assert the builders produce identical bytes, masking fields that legitimately vary.
  - Prior art: the plain NUnit tests in the existing unit test project, such as the character stat validator and drain array tests.
- **Not tested automatically:** driving the Harness client from an in-process integration test. It would need a database and a booted world, and no integration-test harness exists yet. Revisit if later milestones need one.
- The Harness trace isn't asserted on in tests. It's a research artifact, and its content will change whenever the server changes what it sends.

## Out of Scope

- The Mind, the WebSocket connection, and any TypeScript code (milestone 2).
- Any LLM use, Planning, Reflection, Tactics or Lessons.
- Primitives beyond what login, liveness and logout need: movement, combat, chat and the rest.
- Turning traced calls into structured Percepts. Milestone 1 only records them.
- The Journal format. The milestone 1 trace is a precursor, not the Journal.
- Character creation by an Agent. The Human creates the Agent's Account and Character (a Celt Blademaster in Hibernia) with the real client beforehand.
- More than one Agent online at once, beyond what the registry naturally allows. No scale testing.
- Client versions other than 1.127.
- Any change to how Human clients connect or behave.

## Further Notes

- Vocabulary follows the root glossary (Agent, Human, Possession, Harness, Mind, Primitive, Percept). "Body" and "brain" are deliberately not used, because they clash with the server's NPC AI.
- Relevant decisions: ADR 0001 (Percepts via a custom packet library), ADR 0002 (Primitives through real packet handlers), ADR 0003 (the Mind as a separate TypeScript process, which is what milestone 2 builds towards).
- Risk: any server code that queues packets on the packet processor directly instead of going through the packet library bypasses the Harness trace (ADR 0001). If the trace looks incomplete, check for this first.
- Risk: some handlers used during world entry may assume a real socket or remote endpoint (for example when logging the client's address). Those need to tolerate a missing socket as part of the core seam work.
- The idle-kick rule only applies to privilege level 1 Accounts. Agent Accounts are normal players, so long idle tests will hit it. That's expected, and it's Human-equivalent behaviour.
- Milestone ladder for context: 1 login/logout (this spec) → 2 Mind connection → 3 movement → 4 hand-written grind Tactic → 5 LLM Planning → 6 Reflection and Tactic Library → 7 Agent character creation → 8 multiple Agents and grouping.

## Comments

**2026-10-20 (Human, recorded by agent):** Milestone 1 is done. All eight tickets are resolved. The acceptance tests in Testing Decisions passed: the manual login, idle and logout checks (tickets 03 to 05), and `/agent soak` with a clean server log (ticket 08). That was 50 cycles from the server console, plus an in-game run the Human stopped after 21 cycles. Next on the ladder is milestone 2 (Mind connection).
