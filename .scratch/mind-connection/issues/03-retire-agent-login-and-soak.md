# 03: Retire `/agent login` and `/agent soak`; `/agent logout` kicks the Mind

**What to build:** Starting a Mind is now the only way an Agent enters the world. `/agent login` and `/agent soak` are removed, along with the soak runner. `/agent logout <char>` still quits the Agent through the real `/quit` path. Once its Harness client disconnects, the Agent's Mind connection is closed and the Mind exits. `/agent capture` is unchanged, and `/agent` stays admin-only. See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] `/agent login` and `/agent soak` are gone from the command and its help text, and the soak runner is removed
- [ ] `/agent logout <char>` on an Agent with a connected Mind makes it quit through the normal path, and its Mind exits after the server disconnects it
- [ ] `/agent logout` on a Character that isn't online as an Agent fails with a clear message
- [ ] `/agent capture` still records a real client's packets
- [ ] A privilege level 1 Account can't use `/agent`
