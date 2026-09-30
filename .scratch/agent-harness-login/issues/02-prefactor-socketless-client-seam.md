# 02: Prefactor: core support for a client without a socket

**What to build:** The minimal changes to the core client and packet-processing code that let a game client exist without a network socket. The socket-touching members (receive, send pending packets, close) become overridable, and paths that read socket state or the remote address tolerate a missing socket. There is no behaviour change for Human clients. The changes stay small, generic and in their own commits, so upstream merges stay cheap (and could be proposed upstream as headless-client support). See the spec and ADR 0001/0002.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Receive, pending-packet send and socket close on the game client can be overridden by a derived client type
- [ ] Code on the connect, tick, world-entry and disconnect paths that reads socket state or the remote address doesn't throw when there is no socket
- [ ] A Human can still log in, play and log out with the real client exactly as before
- [ ] Existing unit tests pass
- [ ] The changes are in their own small commits, separate from any Harness code
