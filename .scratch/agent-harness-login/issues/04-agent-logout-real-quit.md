# 04: `/agent logout` through the real quit path

**What to build:** An admin types `/agent logout <char>` and the Agent quits the way a Human does. The Harness sends the real `/quit` command packet, the server's normal quit path runs (including any quit delay), and when the server tells the Harness the player has quit, the Harness completes disconnection the way a real client closes its connection. The Character is saved and removed through the existing path, and the registry entry and session are released. Shutting the server down while an Agent is online behaves as it would for an online Human. See the spec.

**Blocked by:** 03

**Status:** ready-for-human

- [ ] After `/agent logout <char>`, the Character vanishes from the Human's screen and from `/who`
- [ ] The Character's location and state persist: moving it (e.g. with a GM command) and then logging it out and back in restores the new location
- [ ] The Agent never becomes linkdead during logout
- [ ] The client's session and registry entry are released after logout
- [ ] Server shutdown with an Agent online neither hangs nor loses the Agent's state
- [x] A golden-byte unit test shows the `/quit` command packet builder matches the 01 fixture

## Comments

**2026-10-03 (agent, from the 01 capture):** The real client's quit is command 0xAF `&q`. The Character then sits for the 20 s quit delay, and position updates keep flowing with the `SITTING` flag. The client then returns to character select (0xAC, character select `00`, overview `03`) with its connection still open. It closes the connection only when the Human exits from the character screen. Details are in `Tests/Fixtures/Agents/v1127/README.md`. Position updates have to continue through the quit delay, or the Character goes linkdead before the quit completes (see 05).

**2026-10-03 (agent):** The Human typed `/q` during the capture, so the fixture holds `&q`. The Harness sends `/quit` instead, presumably as data `00` `&quit` `00` (not captured, inferred from the `&q` layout). Golden-test the builder against the captured command packet, treating the command text as the varying part. The README lists 0xAF's whole data as varying.

**2026-10-03 (agent):** Built. The remaining checks need the Human with the real client and a running server, so this is back with the Human.

What exists now:
- `/agent logout <char>` (admin only) finds the Character's Harness client in the registry and has it send the `/quit` command packet (`ClientPackets1127.Quit`, `&quit`) through the normal packet processor. It replies "<char> is quitting..." and then "<char> has quit." once the server has disconnected the client. If the Character isn't in the world as an Agent, it says so and does nothing.
- The server's quit path runs unchanged. For a privilege level 1 Account that means the 20 s quit delay with the Character sitting. Liveness (05) keeps sending position updates with `SITTING` during the delay, so the Character doesn't go linkdead.
- `HarnessPacketLib` hands the server's player-quit call to `HarnessClient.OnPlayerQuit`. On the next tick `WorldExit` does what the real client did in the capture: character select `00` and overview, which puts the client at the character screen. One tick later it closes the connection the way a real client's closed socket is noticed, at the receive step. The server then disconnects it through `GameClient.OnDisconnect`: no link death, because the client isn't Playing any more. The session is released, and the Account's last-disconnect time is saved. The Character itself was already saved and removed by the quit path, before the player-quit call returned.
  - This is a small detour from the spec's "triggers the normal client disconnect". The server leaves the client Playing after the quit, so closing right away would count as a link death. The spec now says so.
  - A total-out quit (kicks, ban, crash, speedhack) closes the connection straight away. That wasn't captured; the server disconnects such clients itself anyway.
- `AgentRegistry` releases a Character's entry whenever its Harness client disconnects, however that comes about: logout, timeout, link-death quit.
- Shutdown needed no change. `GameServer.Stop` saves every online player through `ClientService.SavePlayers`, Agents included, and nothing waits on the Harness.
- `UT_ClientPackets1127.Quit_ShouldMatchCapture` checks the `/quit` builder against the captured `&q` command in both captures. Only the command text differs: the captured data is `00 &q 00`, and the builder's data is `00 &quit 00`.

For the Human, when the server runs this build:
1. `/agent login <char>`, then `/agent logout <char>`. After "<char> is quitting..." the Character should sit for about 20 s, then vanish from your screen and from `/who`, and "<char> has quit." should follow. The server log should show "<account> just disconnected." with no "Linkdead player" or "Hard timeout" for it.
2. Log it in again, move it with a GM command (e.g. `/jump <char> to me`, or stand next to it and use `/move`), log it out, log it in again. It should appear at the new spot.
3. `/agent login <char>` right after a logout should work again (the registry entry and session were released).
4. With the Agent online, shut the server down (`/shutdown`). It should stop without hanging. After the restart, `/agent login <char>` should bring it back where it was.

Known gaps:
- **A refused quit hangs the logout.** If the server refuses the quit (dead, mounted, moving, crafting, in an instance, or the command spam check), its reason goes to the Harness packet library and is ignored, so `/agent logout` stays at "quitting..." forever. 07 (failure reporting) and 08 (a cycle that doesn't fully disconnect fails) need a time limit here.
- **An idle kick now ends the connection.** `KickPlayerToCharScreen` also uses the player-quit call, so an idle-kicked Agent now disconnects instead of waiting at the character screen as 05's notes said. That follows the spec's "the server's player-quit call makes the Harness complete disconnection" and releases the registry entry. A Human's client would stay at the character screen, but an Agent has nothing to do there.
