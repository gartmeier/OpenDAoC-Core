# 02: Prefactor: core support for a client without a socket

**What to build:** The minimal changes to the core client and packet-processing code that let a game client exist without a network socket. The socket-touching members (receive, send pending packets, close) become overridable, and paths that read socket state or the remote address tolerate a missing socket. There is no behaviour change for Human clients. The changes stay small, generic and in their own commits, so upstream merges stay cheap (and could be proposed upstream as headless-client support). See the spec and ADR 0001/0002.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Receive, pending-packet send and socket close on the game client can be overridden by a derived client type
- [x] Code on the connect, tick, world-entry and disconnect paths that reads socket state or the remote address doesn't throw when there is no socket
- [x] A Human can still log in, play and log out with the real client exactly as before
- [x] Existing unit tests pass
- [x] The changes are in their own small commits, separate from any Harness code

## Comments

**2026-10-03 (agent):** Done in three core commits (d2abc8305, 0f2a2e1f2, 889449157), with no Harness code:
- `BaseClient.Receive` and `BaseClient.CloseSocket` are virtual. A new virtual `GameClient.SendPendingPackets()` is what the client service's send step now calls. Override these three in the Harness client.
- `PacketProcessor` treats a missing socket like a closed one: queued packets are dropped and sends are skipped. `/gminfo` shows empty endpoints instead of throwing (spec story 4, inspecting the Character).
- `BaseServer.Connect(client)` gives a client without a socket a session ID from the server's own allocator and registers it with the client service, the same way an accepted connection is registered. The allocator is private, so the Harness couldn't do this without a seam. Call `GameServer.Instance.Connect(harnessClient)`.

I traced the paths, and nothing else on them dereferences the socket. Tick, connect, link death, quit and disconnect only read `TcpEndpointAddress`, which already returns `[not connected]`. The world-entry handlers in the captured sequence don't touch the socket. The only direct callers of `PacketProcessor.SendPendingPackets` are the Human packet libraries and the login request handler, and the Harness bypasses both. For a client that has a socket, `Socket?.Connected != true` behaves exactly like `!Socket.Connected`, so Humans are unaffected. All 38 unit tests pass. No new tests were added: the spec agrees only the `/agent` command and golden-byte seams, and ticket 03 exercises this one end to end.

Not verified by the agent: a real-client login, play and logout. Left for the Human.

Known gaps, deliberately left alone:
- `/sameip` (`GameServer/scripts/commands/SameIP.cs`) still dereferences `Socket.RemoteEndPoint`. It throws only if two clients without a socket are online together, which milestone 1 doesn't do.
- `BaseServer.Connect` doesn't raise `GameClientEvent.Created`, which `GameServer.GetNewClient` raises for accepted connections. Nothing listens for that event today.

**2026-10-03 (Human):** Logged in and quit with the real client after these changes. Both work as before.
