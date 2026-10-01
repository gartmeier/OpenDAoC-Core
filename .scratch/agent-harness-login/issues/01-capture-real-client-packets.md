# 01: Capture a real 1.127 client's login and quit packets

**What to build:** A recorded, checked-in reference of exactly what a real 1.127 game client sends inbound from character select through world entry, for `/quit`, and for pings. It includes each packet's order, type and bytes, and notes which fields legitimately vary between captures (e.g. timestamps, session-dependent values). These become the fixtures for every Harness packet builder's golden-byte test and the source of truth for the login sequence the Harness replays. See the spec: `.scratch/agent-harness-login/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] A capture exists of a real 1.127 client logging an Agent Account's Character in from character select, idling long enough to send pings, and quitting via `/quit`
- [ ] The world-entry sequence (packet types in order) is documented next to the fixtures
- [ ] Fixtures for each packet the Harness will need (overview/realm, character select, world init and the rest of world entry, ping, `/quit` command) are checked in to the test project
- [ ] Fields that legitimately vary between captures are identified for each fixture
- [ ] Capturing adds no permanent behaviour change for Human clients (any capture tooling is either removed or off by default)

## Comments

**2026-10-01 (agent):** The capture tooling is built, off by default and in-memory only. The capture itself needs a Human at the real client, so this ticket is back with the Human. Once the capture files exist, hand it back to an agent to turn them into fixtures and docs.

What exists now:
- `/agent capture <account>` (admin) records every packet a real client logged into `<account>` sends. It writes the raw wire bytes (header, data, checksum), transport, client state and elapsed ms to `Debug/packet-captures/<account>-<timestamp>.tsv`, which is git-ignored. `/agent capture off` stops it, and a server restart also turns it off.
- `GameServer/agents/CapturedPacket.cs` defines the line format and parses it back (`CapturedPacket.ParseLines`). The fixtures will use the same format.
- The core hooks are one line each, in the TCP receive path (`GameClient.ProcessInboundTcpBytes`) and the UDP receive path (`GameServer.OnUdpReceive`), and live in their own commit. When capture is off, each costs a null check. They're not one of the spec's listed seams: they can be reverted once the fixtures are in, or kept for future recaptures.

Capture steps for the Human:
1. Build and start the server as usual.
2. Log in as an admin and run `/agent capture agent1`, using the Agent Account's name. It replies with the file path.
3. Exit, then log into the Agent Account with the real 1.127 client. Select its Character and enter the world.
4. Don't touch anything for about 2 minutes, so several pings are sent. Then type `/quit` and let it finish.
5. Repeat steps 3 and 4 once more. A second cycle is what shows which fields legitimately vary.
6. Log in as admin and run `/agent capture off`.

Agent follow-up once the captures exist:
- Curate the captures into fixtures under the test project (copy them to the output directory).
- Add a fixture sanity test: every packet parses and `HasValidChecksum` holds.
- Document the world-entry sequence and the fields that vary per packet next to the fixtures.

Notes for that work:
- Packets sent before the Account is set aren't recorded (the version check and the login request). That's fine, because the Harness skips them.
- UDP packets are posted to the game loop, so the order between TCP and UDP is the server's processing order, not the order on the wire.
- The `elapsed_ms` values can be slightly out of order across transports.
