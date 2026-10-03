# 05: Liveness: the Agent stays online while idle

**What to build:** While online, the Harness answers the server's liveness checks the way a real 1.127 client does, sending pings automatically at the real client's interval, so an idle Agent is never disconnected for timing out and never goes linkdead. The Agent stays subject to the normal ping timeout and to the same idle-kick rules as a Human at its privilege level. See the spec.

**Blocked by:** 03

**Status:** resolved

- [x] An Agent logged in with `/agent login` stays in the world, not linkdead, for longer than several ping-timeout windows
- [x] Pings are sent through the normal packet processor at the interval observed in the 01 capture
- [x] No special-case exemption from ping timeout or idle kick is added for Agents
- [x] A golden-byte unit test shows the ping packet builder matches the 01 fixture

## Comments

**2026-10-03 (agent, from the 01 capture):** Pings alone aren't enough. The server soft link-deads a player whose last position update (0xA9) is more than 5 s old (`PlayerMovementComponent.SOFT_LINK_DEATH_THRESHOLD`). The real 1.127 client sends:
- a ping every 5000 ms
- a position update about every 2130 ms, even when standing still
- a UDP init/ping pair every 65 s; the server falls back to TCP without it

So liveness also needs periodic position updates, repeating the Character's current position. Details and varying fields are in `Tests/Fixtures/Agents/v1127/README.md`.

**2026-10-03 (agent):** Built. The remaining check needs the Human with a running server, so this is back with the Human.

What exists now:
- `Liveness` (in `GameServer/agents`) schedules a ping every 5000 ms and a position update every 2133 ms. Those are the median intervals of the idle packets in both captures (the README's "about 2130"). It keeps to the interval whatever the game loop's tick length is, and after a stall it sends once rather than catching up.
- `HarnessClient` starts it once world entry completes. On each client tick, before the queued packets are processed, it enqueues what's due. Pings go out in any state. Position updates repeat the Character's current position, and only while it's in the world (Playing and active). They carry `SITTING` while the Character sits, as in the quit delay (04).
- UDP init and UDP ping are skipped, because the server falls back to TCP without them.
- Nothing about ping timeout, link death or the idle kick changed. A repeated position with speed 0 doesn't count as activity, so an idle Agent at privilege level 1 is kicked to the character screen after `KICK_IDLE_PLAYER_TIME` minutes, like an idle Human (when `KICK_IDLE_PLAYER_STATUS` is on).
- Tests:
  - `UT_ClientPackets1127.IdlePing_ShouldMatchCapture` checks every idle ping in both captures byte for byte. It masks only data offsets 1–2 (the client's ping cycle, which the server doesn't read).
  - `UT_Liveness` checks the schedule's ping and position update intervals against each capture's median, at 1 ms and 33 ms ticks. It also checks that the schedule doesn't drift over an hour and doesn't catch up after a stall.

For the Human, when the server runs this build:
1. `/agent login <char>`, then leave the Agent alone for at least 3 minutes. That's well past the 5 s soft link-death threshold, and covers several ping intervals.
2. It should stay in `/who` and stay visible and targetable, not linkdead. The server log should show no "Position update timeout" or "Hard timeout" for it.
3. The 150 s hard ping timeout is the longest window. An idle of 10 minutes or more covers several of those. If the idle kick is on, it may send the Agent to the character screen first. That's expected, and pings still keep the connection alive there.

Known gaps:
- **The ping's cycling bytes.** A real client cycles data offsets 1–2 through 11 values, and sets offset 11 on its first world-entry ping. The builder writes 0 there. The server reads neither.
- **Liveness never stops while the Harness client is connected.** 04 will end the connection after the quit, so it doesn't matter yet. Recheck when 04 lands.

**2026-10-03 (Human, recorded by agent):** Logged Greeen in with `/agent login` on this build at 15:58:30 and left him idle. He was still there 50 minutes later, about 20 hard ping-timeout windows. The server log since that login has no "Position update timeout", "Hard timeout", link death or quit for him. The idle kick wasn't exercised: it's off by default (`KICK_IDLE_PLAYER_STATUS`), and even when on it waits 60 minutes by default (`KICK_IDLE_PLAYER_TIME`). All acceptance items are met.
