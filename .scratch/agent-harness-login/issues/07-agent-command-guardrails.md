# 07: Guardrails on `/agent`

**What to build:** `/agent` refuses unsafe or nonsensical requests with clear messages, so mistakes never fail silently or hijack a Human's Character. See the spec.

**Blocked by:** 04

**Status:** ready-for-human

- [ ] `/agent login` with an unknown Character name fails with a clear message
- [ ] `/agent login` on a Character whose Account isn't an Agent Account (no `agent` prefix) is refused
- [ ] `/agent login` on a Character that's already online (Possession by a Human, or another Harness) is refused
- [ ] `/agent logout` on a Character that isn't online as an Agent fails with a clear message and changes nothing
- [ ] A login that doesn't reach in-world play within a time limit is reported as a failure, and the half-connected Harness client is cleaned up
- [ ] Non-admins can't use any `/agent` subcommand

## Comments

**2026-10-04 (agent):** Built. The checks need the Human with the real client and a running server, so this is back with the Human.

What exists now:
- **Unknown name.** `/agent login <char>` with no such Character says "No Character is named '<char>'." This was already there from 03.
- **Human's Account.** A Character whose Account name doesn't start with `agent` is refused, and the reply names the Account. `AgentAccount.IsAgentAccount` is the one place the naming convention lives. It's case-insensitive, like the server's own account-name check. `UT_AgentAccount` covers it.
- **Already online.** `/agent login` refuses a Character whose Account already has a client on the server, the same rule the server's login check applies to a real client:
  - a Harness client: "<char> is already online as an Agent."
  - a real client, at the character screen or in the world: "'<account>' is already logged in with a real client (Possession, as <char>)."
  - A client that connected this tick only shows up from the next tick on. The registry covers a second `/agent login` of the same Character in that window.
- **Logout of a Character that isn't online as an Agent.** "<char> isn't in the world as an Agent." Nothing is sent. This was already there from 04.
- **Login time limit.** World entry fails if the Character isn't in the world 30 s after `/agent login`. It also fails if the server disconnects the Harness client before then, for example when `AddToWorld` fails. Either way `/agent login` reports it, e.g. "Greeen didn't reach the world within 30 s (client CharScreen, Character not loaded).", then "Greeen's Harness client has disconnected. The trace at … shows what the server told it." once the cleanup is done.
  - The check runs on the client tick, on the game loop, so it can't race with success. A Character already in the world is never failed.
  - Cleanup is the client closing its connection, which the server handles as a disconnect: it saves the Character and quits it if it was in WorldEnter.
  - If the time limit runs out while the server is still loading the Character, the Harness waits for the load to finish before closing. The world-init handler carries on after the load whatever the client's state, so closing earlier would leave a ghost Character in the world. A load that never finishes leaves the client at the character screen until the server's 150 s ping timeout disconnects it.
- **Logout time limit** (04's known gap). If the server hasn't disconnected the client 90 s after `/agent logout`, the command reports "<char> hasn't quit within 90 s. Either the server refused the quit and <char> stays in the world, or combat keeps restarting the quit delay. The trace at … shows which." 90 s is the longest quit delay (60 s) plus a margin. Nothing is undone: a quit delay that's still running still ends in a quit.
- **Admins only.** Unchanged. `/agent` is declared `ePrivLevel.Admin`, and the server's command dispatcher refuses lower privilege levels for every subcommand. The only exception is an explicit single-command grant (`SinglePermission`), which is an admin's own choice.

For the Human, when the server runs this build:
1. `/agent login nosuchname` should say no Character has that name.
2. `/agent login <one of your own Characters>` should be refused, naming your Account as not an Agent Account.
3. `/agent login greeen` twice: the second should say he's already online as an Agent. Then `/agent logout greeen`.
4. `/agent logout greeen` while he's offline should say he isn't in the world as an Agent.
5. Possession: log into `agent1` with a second real client and stay at the character screen (or enter as Greeen). Then `/agent login greeen` from your admin Character should be refused as logged in with a real client. Skip this if the server won't let two real clients in at once.
6. With a privilege level 1 Account, `/agent login greeen` should be refused like any other admin command.
7. Optional, refused quit: get Greeen into a state where quitting is refused (for example dead, or mounted), then `/agent logout greeen`. After 90 s it should report that he hasn't quit, and the trace should show the server's reason.
8. Optional, login time limit: there's no simple in-game trigger. With a debugger attached, a breakpoint in `GameClient.LoadPlayer` held for more than 30 s should produce the "didn't reach the world within 30 s" message, then the disconnect message once the load finishes. Greeen should not be left in the world or in `/who` afterwards.

Known gaps:
- **Two Characters on one Agent Account in one tick.** The Account check misses a client that connected this tick, and the registry is keyed by Character name. Logging in two different Characters of the same Agent Account in the same tick would get both through. An Agent Account has one Character, so this is left alone.
- **The ghost-Character hazard is the server's, not the Harness's.** A real client that drops during the character load hits the same unguarded continuation in `WorldInitRequestHandler`. The Harness avoids it, but the server code isn't changed.
- **"Agent Account" isn't in `CONTEXT.md` yet.** The spec uses the term throughout. Worth adding with its `agent` prefix rule.
