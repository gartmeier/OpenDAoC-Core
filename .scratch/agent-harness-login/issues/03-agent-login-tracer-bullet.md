# 03: Tracer bullet: `/agent login` puts an Agent's Character in the world

**What to build:** An admin types `/agent login <char>` and the Agent's Character enters the world through the Harness, with no real client, replaying the captured 1.127 sequence from character select onwards through the real packet handlers, at the same point in the client tick as a Human's packets.
- The Harness client carries the Agent's Account and a Harness packet library (no-op for now), so the network handshake, version check and password check are skipped.
- It registers like a real connection, including session allocation.
- A minimal in-memory registry tracks which Characters are online as Agents.
- Login completion is detected from the client reaching in-world play (world entry loads the character asynchronously), and only then is success reported.

Without liveness (05) the Agent will eventually be timed out. That's expected here. See the spec and ADR 0001/0002.

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [ ] `/agent login <char>` is available only to admin privilege
- [ ] After the command, the Agent's Character appears at its saved location next to the Human, shows in `/who`, and can be targeted and inspected with the real client
- [ ] Inbound packets go through the normal packet processor and handlers, not direct player method calls
- [ ] The success message is shown only once the Character is actually in the world
- [ ] Golden-byte unit tests show every login packet builder matches the 01 fixtures (varying fields masked)
- [ ] Harness code lives in its own folder and namespace; core changes are limited to what 02 introduced

## Comments

**2026-10-03 (agent, from the 01 capture):** The real world-entry sequence is in `Tests/Fixtures/Agents/v1127/README.md`. One catch for this ticket: the server soft link-deads a player whose last position update (0xA9) is more than 5 s old (`PlayerMovementComponent.SOFT_LINK_DEATH_THRESHOLD`). A Harness that only enters the world will show its Character linkdead within seconds. The real client sends a position update about every 2.1 s, even when standing still. This ticket either needs position updates too, or has to accept being linkdead until 05 lands.
