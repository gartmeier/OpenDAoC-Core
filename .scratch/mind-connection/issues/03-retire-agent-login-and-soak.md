# 03: Retire `/agent login` and `/agent soak`; `/agent logout` kicks the Mind

**What to build:** Starting a Mind is now the only way an Agent enters the world. `/agent login` and `/agent soak` are removed, along with the soak runner. `/agent logout <char>` still quits the Agent through the real `/quit` path. Once its Harness client disconnects, the Agent's Mind connection is closed and the Mind exits. `/agent capture` is unchanged, and `/agent` stays admin-only. See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** 02

**Status:** ready-for-human

- [ ] `/agent login` and `/agent soak` are gone from the command and its help text, and the soak runner is removed
- [ ] `/agent logout <char>` on an Agent with a connected Mind makes it quit through the normal path, and its Mind exits after the server disconnects it
- [ ] `/agent logout` on a Character that isn't online as an Agent fails with a clear message
- [ ] `/agent capture` still records a real client's packets
- [ ] A privilege level 1 Account can't use `/agent`

## Comments

**2026-10-20 (agent):** Built on branch `claude/retire-agent-login-1aff8b`. The checks need the Human with the real client, so this is with the Human.

What exists now:
- **`/agent`** has two subcommands, `logout <char>` and `capture <account>`, and its help text lists only those. `login` and `soak` print the syntax like any unknown subcommand. It's still `ePrivLevel.Admin`, unchanged.
- **The soak runner is gone**: `Soak.cs`, `SoakResult` and `UT_Soak`. `AgentLogin.TryStart` keeps only the Mind's entry point, which no longer hands back a world-entry task, since the Harness client tells the Mind itself (`entered` or `refused`).
- **Every Harness client has a Mind.** `HarnessClient`'s constructor takes the link without a default, and the `Mind?.` checks are plain calls. The link-less path, whose liveness never stopped because it had no Mind to lose, is gone. A linked client behaves as before.
- **`/agent logout` ends the Mind's connection** through the path ticket 02 already built, not a second one. It queues the same `/quit` as the `quit` Primitive. When the server disconnects the Harness client, its disconnect handler closes the link normally after the queued Percepts, and the Mind ends as `left`. The command now reports "<name> has quit, and the server is closing its Mind's connection." A Character that isn't in the world as an Agent still gets "<char> isn't in the world as an Agent."
- **Checked against a headless server from this branch's build** (09:39–09:40), driven from the console:
  - `bun mind/agent.ts greeen` got Greeen into the world.
  - `/agent login greeen`, `/agent soak greeen 1` and a bare `/agent` each printed the syntax, which lists only `logout` and `capture`.
  - `/agent logout nobody` printed "nobody isn't in the world as an Agent."
  - `/agent logout greeen` printed "Greeen is quitting...". He sat through the quit delay. The Mind's last Percepts were `SendPlayerQuit`, `SendLoginGranted`, `SendSessionID` and `SendCharacterOverview`. Then "agent1 just disconnected.", with no link death, and "Greeen's Mind has disconnected." The Mind printed "The server has disconnected greeen." and exited with code 0.
- Tests: server, 132 passing (the soak tests are gone, nothing else changed). No new automated test: the Harness client's disconnect needs a booted world, and each half is already covered, the link's normal close after its queue drains by `UT_MindLink` and the Mind ending as `left` on a normal close by `mind/src/connection.test.ts`.

For the Human, when the server runs this build:
1. `/agent` in game: the help lists only `logout` and `capture`. `/agent login greeen` and `/agent soak greeen 1` just print that help.
2. Start `bun mind/agent.ts greeen`, then `/agent logout greeen` in game. Greeen sits through the quit delay and vanishes without going linkdead. You see "Greeen has quit, and the server is closing its Mind's connection." The Mind prints "The server has disconnected greeen." and exits, and its terminal tab stays open.
3. `/agent logout nobody`, and `/agent logout greeen` with no Mind running: each says "<name> isn't in the world as an Agent."
4. `/agent capture admin`, a few actions with the real client, then `/agent capture off`. A new file under `Debug/packet-captures` holds the client's packets.
5. Log in with a privilege level 1 Account and type `/agent`. The server refuses it as an unknown command.

Known gaps:
- **A linkdead Agent can't be logged out.** `/agent logout` on a Character that's linkdead says it isn't in the world as an Agent, because only a Playing client can send `/quit`. It's gone after the link-death window anyway.
- **The Mind's name is as typed** ("greeen"), as ticket 02 noted.
