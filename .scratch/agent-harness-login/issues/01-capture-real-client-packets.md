# 01: Capture a real 1.127 client's login and quit packets

**What to build:** A recorded, checked-in reference of exactly what a real 1.127 game client sends inbound from character select through world entry, for `/quit`, and for pings. It includes each packet's order, type and bytes, and notes which fields legitimately vary between captures (e.g. timestamps, session-dependent values). These become the fixtures for every Harness packet builder's golden-byte test and the source of truth for the login sequence the Harness replays. See the spec: `.scratch/agent-harness-login/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A capture exists of a real 1.127 client logging an Agent Account's Character in from character select, idling long enough to send pings, and quitting via `/quit`
- [ ] The world-entry sequence (packet types in order) is documented next to the fixtures
- [ ] Fixtures for each packet the Harness will need (overview/realm, character select, world init and the rest of world entry, ping, `/quit` command) are checked in to the test project
- [ ] Fields that legitimately vary between captures are identified for each fixture
- [ ] Capturing adds no permanent behaviour change for Human clients (any capture tooling is either removed or off by default)
