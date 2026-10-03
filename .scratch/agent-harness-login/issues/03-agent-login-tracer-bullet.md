# 03: Tracer bullet: `/agent login` puts an Agent's Character in the world

**What to build:** An admin types `/agent login <char>` and the Agent's Character enters the world through the Harness, with no real client, replaying the captured 1.127 sequence from character select onwards through the real packet handlers, at the same point in the client tick as a Human's packets.
- The Harness client carries the Agent's Account and a Harness packet library (no-op for now), so the network handshake, version check and password check are skipped.
- It registers like a real connection, including session allocation.
- A minimal in-memory registry tracks which Characters are online as Agents.
- Login completion is detected from the client reaching in-world play (world entry loads the character asynchronously), and only then is success reported.

Without liveness (05) the Agent will eventually be timed out. That's expected here. See the spec and ADR 0001/0002.

**Blocked by:** 01, 02

**Status:** ready-for-human

- [x] `/agent login <char>` is available only to admin privilege
- [ ] After the command, the Agent's Character appears at its saved location next to the Human, shows in `/who`, and can be targeted and inspected with the real client
- [x] Inbound packets go through the normal packet processor and handlers, not direct player method calls
- [x] The success message is shown only once the Character is actually in the world
- [x] Golden-byte unit tests show every login packet builder matches the 01 fixtures (varying fields masked)
- [x] Harness code lives in its own folder and namespace; core changes are limited to what 02 introduced (plus the packet capture hooks from 01, which the Human chose to keep)

## Comments

**2026-10-03 (agent, from the 01 capture):** The real world-entry sequence is in `Tests/Fixtures/Agents/v1127/README.md`. One catch for this ticket: the server soft link-deads a player whose last position update (0xA9) is more than 5 s old (`PlayerMovementComponent.SOFT_LINK_DEATH_THRESHOLD`). A Harness that only enters the world will show its Character linkdead within seconds. The real client sends a position update about every 2.1 s, even when standing still. This ticket either needs position updates too, or has to accept being linkdead until 05 lands.

**2026-10-03 (agent):** Built. The in-game checks need the Human with the real client, so this is back with the Human.

What exists now, all in `GameServer/agents` (namespace `DOL.GS.Agents`) plus the `login` subcommand in `/agent` (admin only):
- `HarnessClient` is a `GameClient` without a socket. `EnterWorld` gives it the Agent's Account, version 1.127 and `HarnessPacketLib`, sets it to Connecting (where a real login request leaves it), and calls `GameServer.Instance.Connect`, so it gets a session ID and registers like an accepted connection. Its `Receive` override drains a queue of synthesized packets through `PacketProcessor.ProcessInboundPacket` at the client tick's receive step. Sending and closing the socket are no-ops.
- `HarnessPacketLib` is a `DispatchProxy` over `IPacketLib`, so every call the server makes arrives in one method; 06 can trace it there. For now it ignores every call. It returns the real animation IDs for the four properties and `false` from `SendLosCheckRequest`, which is what a real library returns when the client isn't Playing.
- `WorldEntry` replays the README's world-entry table in three bursts, each held back until the client state shows the server is ready for it:
  1. Character select through world init.
  2. Player init, ping, position, game open. Sent once the async character load has put the Character in the world (WorldEnter and active).
  3. Filter settings, `&gc info 1`, warmap request, position. Sent once Playing.

  It completes once the client is Playing with an active Character, and only then does `/agent login` say "<char> is in the world." It leaves out the packets with no handler (0x1D, 0x42, 0x43) and the create object request (0xBE), which reacts to a specific object the server sent.
- `ClientPackets1127` builds every packet above. `UT_ClientPackets1127` checks each one against the world-entry packets of both captures (including the ping, which 05 also asks for). Sequence and session ID come from the captured packet. Wire byte 8, the checksum and the README's varying data offsets are masked.
- `AgentRegistry` maps Character names to Harness clients and refuses a second login of a Character whose Harness client hasn't disconnected.

For the Human, when the server runs this build:
1. `/agent login <char>` replies "Logging <char> in through the Harness..." and then "<char> is in the world."
2. **Be quick.** There are no position updates yet (05), so the Character soft link-deads after 5 s and is quit about 60 s later. That leaves about a minute to see it at its saved spot, find it in `/who`, and target and inspect it.
3. Once it has been quit, `/agent login` works again for the same Character.

Known gaps, deliberately left for later tickets:
- **Failure reporting (07).** If world entry never completes (for example if `AddToWorld` fails), `/agent login` reports nothing. The only messages added here are "no Character is named …", "Account doesn't exist" and "already online as an Agent", because the command can't run without them.
- **`GameClient.ClientType` stays Unknown.** The real client sets it in the version and crypt-key request (0xF4), which wasn't captured. It only matters for expansion-gated region changes and jumps.
- **No-op side effects.** A few real `Send*` methods change server state as a side effect: quest list indices, custom dialog callbacks, the trainer skill cache and line of sight checks. The no-op library skips those changes. None of them is on the login path.
- **ADR 0002's round-trip wording.** The ADR says builder tests should "round-trip through the handlers' read logic". The tests compare against the captured bytes instead. Those are the bytes the handlers already read from a real client, but it's worth deciding whether the ADR wording should change.
