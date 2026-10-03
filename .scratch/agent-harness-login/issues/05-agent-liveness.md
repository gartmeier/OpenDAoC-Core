# 05: Liveness: the Agent stays online while idle

**What to build:** While online, the Harness answers the server's liveness checks the way a real 1.127 client does, sending pings automatically at the real client's interval, so an idle Agent is never disconnected for timing out and never goes linkdead. The Agent stays subject to the normal ping timeout and to the same idle-kick rules as a Human at its privilege level. See the spec.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] An Agent logged in with `/agent login` stays in the world, not linkdead, for longer than several ping-timeout windows
- [ ] Pings are sent through the normal packet processor at the interval observed in the 01 capture
- [ ] No special-case exemption from ping timeout or idle kick is added for Agents
- [ ] A golden-byte unit test shows the ping packet builder matches the 01 fixture

## Comments

**2026-10-03 (agent, from the 01 capture):** Pings alone aren't enough. The server soft link-deads a player whose last position update (0xA9) is more than 5 s old (`PlayerMovementComponent.SOFT_LINK_DEATH_THRESHOLD`). The real 1.127 client sends:
- a ping every 5000 ms
- a position update about every 2130 ms, even when standing still
- a UDP init/ping pair every 65 s; the server falls back to TCP without it

So liveness also needs periodic position updates, repeating the Character's current position. Details and varying fields are in `Tests/Fixtures/Agents/v1127/README.md`.
