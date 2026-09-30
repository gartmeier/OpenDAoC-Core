# 04: `/agent logout` through the real quit path

**What to build:** An admin types `/agent logout <char>` and the Agent quits the way a Human does. The Harness sends the real `/quit` command packet, the server's normal quit path runs (including any quit delay), and when the server tells the Harness the player has quit, the Harness completes disconnection the way a real client closes its connection. The Character is saved and removed through the existing path, and the registry entry and session are released. Shutting the server down while an Agent is online behaves as it would for an online Human. See the spec.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] After `/agent logout <char>`, the Character vanishes from the Human's screen and from `/who`
- [ ] The Character's location and state persist: moving it (e.g. with a GM command) and then logging it out and back in restores the new location
- [ ] The Agent never becomes linkdead during logout
- [ ] The client's session and registry entry are released after logout
- [ ] Server shutdown with an Agent online neither hangs nor loses the Agent's state
- [ ] A golden-byte unit test shows the `/quit` command packet builder matches the 01 fixture
