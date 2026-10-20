# 05: Who gets in: takeover, link-death retry and refusals

**What to build:** The Mind endpoint decides which Mind drives an Agent, the way ADR 0005 says:
- A second Mind for an Agent that's online with a live Mind takes over the connection without a logout. The old Mind gets `replaced` and exits.
- A Mind for an Agent that's still linkdead is refused with retry set. The Mind says it's retrying, tries again every few seconds, and logs in once the server has removed the linkdead Character.
- While a Mind is connected, a real client logging into the Agent's Account is refused, as for any Account already online.
- A Mind whose first message isn't a valid `hello`, or that sends a wrong token or an unknown message, is refused with a clear reason. Nothing it sends can affect the Agent or the server.

See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** 02

**Status:** resolved

- [x] Starting a second Mind for an online Agent hands it the connection, the first Mind reports it was replaced and exits, and the Character stays in the world throughout
- [x] Starting a Mind while its Agent is linkdead makes it retry until the Character is gone, then log in
- [x] Logging into the Agent's Account with the real client while a Mind is connected is refused
- [x] A wrong token, a missing or malformed `hello`, and unknown or malformed messages each get a clear refusal and a closed connection, with no effect on any Agent
- [x] Refusals that shouldn't be retried make the Mind exit with the reason

## Comments

**2026-10-20 (agent):** Built on branch `claude/who-gets-in-4c0ce9`. The checks need the Human with a running server and the real client, so this is with the Human.

What exists now:
- **Who gets in.** `AgentLogin.TryStart` now returns an outcome: entering the world, took over, or refused with whether to retry. Milestone 1's checks come first, unchanged. Then, if the registry has a Harness client for the Character that hasn't disconnected, the new Mind asks that client to take over (`HarnessClient.TakeOver`):
  - **Takeover.** If the client's Mind is live, the client is connected to the new Mind instead, without a logout. The old Mind gets the new `replaced` message, and its connection closes once everything queued before it is sent. The new Mind hears `entered` straight away if the Character is in the world, or when world entry completes if it's still entering. The Character stays where it is, and liveness, Percepts and Primitives carry on through the new connection. Percepts for the new Mind count from `#1` again, with times since its own hello. A Primitive the old Mind sends before it hears `replaced` isn't queued.
  - **Link-death retry.** If the client's Mind is lost, or the client is linkdead, the new Mind is refused with retry set: "Greeen is linkdead: its last Mind's connection was lost. The server removes the Character once its link-death window has run out, and a new Mind can log in after that." Once the server has removed him, the registry no longer has him and the next try logs in as usual. A client that's leaving the world (the server has quit the Character and the client is about to disconnect) is refused with retry too.
- **Possession: nothing added.** The server's own login check (`LoginRequestHandler`) finds the Account's online client through `ClientService.GetClientFromAccountName`, which includes Harness clients: they connect through `GameServer.Connect` like any client and have their Account set first. So a real client logging into an Agent's Account is refused with "account already logged in" while a Mind is connected, and "account is in logout procedure" while the Agent is linkdead, as for any Account already online.
- **Refusals** (`MindConnection`). Each comes with what's wrong, and the server closes the connection normally after it:
  - A first message that isn't a hello: "The first message must be a hello with the token and the Agent's Character." plus what was wrong with it, for example "It isn't JSON: ...", or the unknown `type` it named.
  - No hello within 10 s of connecting: "The Mind sent no hello within 10 s of connecting."
  - A wrong token, no token configured, or a hello without a Character, as before.
  - After the hello: an unknown message type or Primitive, a value of the wrong kind (the error names its path, such as `$.id`), a `primitive` message without a Primitive, a second hello, a binary frame, or a message over 64 KiB. Closing the connection then makes the Agent's Mind lost, so the Agent goes linkdead, as when the server drops a real client. The message itself does nothing.
  - Anything the Mind sends after a refusal is ignored. A Mind that doesn't answer the server's close within 5 s is dropped, so no refused, replaced or finished connection stays open.
  - None of the refusals before the hello's checks have passed touches an Agent.
- **Protocol.** `replaced` is a server message with no fields. `MindProtocol.Read` can now say why it couldn't read a message. `mind/src/protocol.ts` is regenerated.
- **The Mind** (`mind/src/connection.ts`):
  - While a refusal says to retry, it prints "Refused: <reason> Retrying in 5 s. Ctrl+C stops." and connects again 5 s later, until it gets in or is refused for good. Ctrl+C while waiting prints "Stopped retrying." and exits with 1.
  - On `replaced` it prints "Another Mind has taken over greeen's connection, so this one is done." and exits with 0, as when its Agent leaves the world. Every other ending still exits with 1.
  - Any other refusal prints the reason and exits, as before.
  - `mind/README.md` describes who gets in.
- Tests:
  - Server, 163 passing. New: `UT_HarnessClient` (a takeover connects the client to the new Mind and tells the old one it was replaced; a client whose Mind is lost refuses the takeover as linkdead; a replaced Mind's Primitive isn't queued), `UT_MindLink` (replace; refusal with retry), `UT_MindProtocol` (writing `replaced`; why a message can't be read) and `UT_MindConnection` (a first message that isn't JSON, no hello in time, a login refused with retry, and every kind of bad message after the hello, each refused and closed with what's wrong, leaving the Agent's link lost). The login is now a seam of `MindConnection.Run`, so those tests stand in for it without a game loop. `UT_MindProtocolTypeScript` passes on the regenerated file.
  - Mind, 17 passing in `bun test` (4 new: retrying until let in, saying so; no retry when the refusal doesn't say to; Ctrl+C while waiting stops; `replaced` ends the Mind without reconnecting). `bun run typecheck` is clean.
  - Against a fake server, `bun mind/agent.ts greeen` printed the two retry lines, got in on the third try, then printed the takeover line and exited with 0.
  - Not run against a real server: the Human's server was running, and a second one would clash on its ports.

For the Human, when the server runs this build (the Mind setup from ticket 02 is unchanged):
1. **Takeover with two Minds.** Start `bun mind/agent.ts greeen` in one terminal tab and wait for "greeen is in the world.". Start the same command in a second tab.
   - The first tab prints "Another Mind has taken over greeen's connection, so this one is done." and exits.
   - The second prints "greeen is in the world." straight away, then Percepts from `#1`.
   - Greeen stays standing where he was: no linkdead message, no quit countdown, no re-entry.
   - The server log shows "A new Mind has taken over Greeen's connection, and Greeen stays in the world." and then "Greeen's old Mind has disconnected, after a new one took over."
   - `/say hello` next to him: he says it back once, from the second Mind only.
   - Leave him for at least a minute: he doesn't go linkdead, so liveness runs through the new connection.
   - Ctrl+C on the second Mind quits him through the normal path.
2. **A Mind started while Greeen is linkdead.** Start a Mind, then kill its process (close its tab, or `Stop-Process` on `bun`). Greeen goes linkdead. Start a Mind straight away.
   - It prints "Refused: Greeen is linkdead: ... Retrying in 5 s. Ctrl+C stops." every 5 s.
   - About 60 s after the kill, once the server has removed and saved him, it prints "greeen is in the world." and he appears where he was.
   - Do it once more and press Ctrl+C while it's retrying: it prints "Stopped retrying." and exits.
3. **The real client refused while a Mind is connected.** With a Mind connected, log into Greeen's Account (`agent1`) with the real client. You're refused as already logged in, and the server log shows "User already logged in!". Greeen and his Mind carry on as before.
4. **Refusals.**
   - A wrong `MIND_TOKEN`: "Refused: The token is wrong. ..." and the Mind exits at once, without retrying. The same for a Character on a Human's Account.
   - The rest need a hand-made client. Save this as `mind/poke.ts` (don't commit it):
     ```ts
     import { loadEnvFile } from "./src/env.ts";

     await loadEnvFile(new URL(".env", import.meta.url));
     const hello = JSON.stringify({ type: "hello", token: process.env.MIND_TOKEN, character: "greeen" });
     const cases: Record<string, string[]> = { silent: [], garbage: ["not json"], dance: [hello, '{"type":"dance"}'], twice: [hello, hello] };
     const messages = cases[Bun.argv[2] ?? ""] ?? [];
     const socket = new WebSocket(process.env.MIND_SERVER_URL || "ws://localhost:10310/mind");
     socket.onopen = () => messages.forEach((message, i) => setTimeout(() => socket.send(message), i * 3000));
     socket.onmessage = (event) => { if (!String(event.data).includes('"percept"')) console.log(String(event.data)); };
     socket.onclose = (event) => console.log(`closed ${event.code}`);
     ```
   - `bun mind/poke.ts silent` sends nothing: after 10 s it prints the refusal "The Mind sent no hello within 10 s of connecting." and `closed 1000`. Then Ctrl+C.
   - `bun mind/poke.ts garbage`: a refusal starting "The first message must be a hello ... It isn't JSON", then `closed 1000`. Greeen stays offline.
   - With Greeen offline, `bun mind/poke.ts dance` logs him in, and 3 s later sends an unknown message. It prints `entered`, then a refusal naming `'dance'`, then `closed 1000`. Greeen then goes linkdead, because the server dropped his connection, and is gone 60 s later.
   - `bun mind/poke.ts twice` does the same with a second hello: "The Mind sent a second hello. ..."
   - The server keeps running throughout, and nothing appears in the log beyond the refusal and, for the last two, the link death.

Known gaps:
- **A Mind refused after its hello leaves its Agent linkdead.** The Mind's connection is the Agent's (ADR 0005), so closing it is a link death, and a restarted Mind retries until the Character is gone. The bad message itself has no effect.
- **A takeover during the quit delay** hands over a Character that's quitting: the new Mind ends with "The server has disconnected greeen." when the quit completes. Once the server has quit him, a new Mind is refused with retry instead.
- **The server logs every refusal**, so a Mind retrying through a link death leaves about a dozen "was refused" lines.
- **The real client's refusal doesn't say why.** It's the client's own "account already logged in" message.
- **The name in the Mind's status lines is as typed** ("greeen"), as ticket 02 noted.

**2026-10-20 (Human, recorded by agent):** Ran the checks above on a server built from main at `5f4057b38` and reports all of them passed. The server log agrees:
- **Takeover** at 13:13:48: a second Mind took over Greeen's connection, the old one disconnected, and Greeen stayed in the world.
- **Link-death retry:** the Mind's connection was lost at 13:14:17 and Greeen went linkdead. A new Mind was refused with retry every 5 s from 13:14:18 to 13:15:13, and logged in at 13:15:18, 61 s after the loss.
- **Real client refused** at 13:16:10 ("User already logged in!") while a Mind was connected.
- **Refusals the Mind exits on**, 13:17:59 to 13:18:27: a wrong token (twice), `vue` on the admin Account (twice) and `nobody`.
- **Hand-made client (`poke.ts`):**
  - `silent` was refused at 13:20:01 after 10 s without a hello.
  - `garbage` was refused at 13:21:00 as not JSON, and no Agent was touched.
  - `dance` at 13:21:10: its valid hello took over from the echo Mind still running from 13:15:18. Its unknown message was refused at 13:21:13 and Greeen went linkdead, then quit at 13:22:13.
  - `twice` logged Greeen in at 13:32:45, was refused at 13:32:48 for its second hello, and Greeen went linkdead.
- The server kept running throughout, with no exceptions from the Harness.

All acceptance items are met.
