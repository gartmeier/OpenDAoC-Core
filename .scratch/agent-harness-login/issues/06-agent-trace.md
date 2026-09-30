# 06: Trace of everything the server tells the Agent

**What to build:** The Harness packet library records every call the server makes to tell the Agent's client something, per Agent, with order and time, into a trace the Human can read after a session. Recording never blocks or slows the game loop (calls arrive on the game loop thread, and writing happens off it). The trace is the inventory of future Percepts. It's a research artifact, not the Journal. See the spec and ADR 0001.

**Blocked by:** 03

**Status:** ready-for-agent

- [ ] After a login/idle/logout session, a per-Agent trace exists listing each server-to-client call in order with a timestamp and its salient arguments
- [ ] A trace of one full world entry is readable and shows the sequence the server sends
- [ ] Recording does no file or network I/O on the game loop thread
- [ ] Several Agents' traces don't interleave into one another
