# rs-sdk: how it shapes a game API for LLM coding agents

Research for milestone 2 (the Mind connection). Primary sources only, read on 2026-10-20:

- **rs-sdk** at commit `b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1` (committed 2026-10-19). Every `rs:` link below points at this commit.
- **RuneBench** repo (`MaxBittker/runebench`) at commit `3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8` (committed 2026-10-13). The rs-sdk README links to `MaxBittker/rs-bench`, and the GitHub API reports that name as moved to `runebench`. Every `rb:` link points at this commit.
- **RuneBench website**, https://maxbittker.github.io/runebench/, fetched 2026-10-20. It has no version, so its content may change.
- **The author's talk** was not available to me. No transcript or slides are in either repo, so nothing here comes from the talk. The website's "Harness Development" section is the closest written account of lessons learned.

Line numbers are given as `L12-34` next to a file link.

## Summary

rs-sdk lets coding agents play a RuneScape-2004 emulator (LostCity) by writing TypeScript against two classes:

- **`BotSDK`** (`sdk`) is the "plumbing". It holds the latest world-state snapshot and provides one `send*` method per client interaction. A successful `send*` only means the game client dispatched the command.
- **`BotActions`** (`bot`) is the "porcelain". Each method resolves a target, walks there, dispatches, and then waits on state predicates for evidence that the effect happened. It returns a typed result with `success`, `message` and usually a `reason` code.

The SDK never talks to the game server. It sends JSON over a WebSocket to a gateway, which routes by username to a real game client: a browser "botclient" tab or a headless "lite" client. That client builds the state snapshot from its own memory and runs the actions through its own input code.

LLMs reach the SDK in two ways:

- The MCP tool `execute_code` runs a TypeScript snippet with `bot` and `sdk` as globals. It cancels any earlier run for the same bot and appends a world-state summary to the result.
- Standalone `runScript` files cover longer runs.

There is no sandbox: snippets run through `new Function` inside the MCP server process. Chat is shown to agents by default, with a `SHOW_CHAT=false` opt-out.

Supporting material:

- Agents learn the game from a single `CLAUDE.md` (symlinked as `AGENTS.md`/`GEMINI.md`), hand-written `learnings/` snippets and a partly generated `wiki/`.
- Agents file SDK papercuts with a one-line CLI that appends to a write-only JSONL file on the demo server.
- `sdk/API.md` is generated from the TypeScript syntax tree, and a unit test in CI keeps it in sync.
- RuneBench runs this stack in Harbor tasks and scores the peak XP rate over 15-second sample windows.

---

## 1. Two layers

### Names, typing, documentation

- The layers are named **Plumbing (BotSDK)** and **Porcelain (BotActions)**. `BotSDK` lives in `sdk/index.ts` and `BotActions` in `sdk/actions.ts`. Code sees them as the `sdk` and `bot` globals. ([rs:sdk/README.md][sdk-readme] L189-211, [rs:CLAUDE.md][claude] L176-183, L314-328)
- **Low-level methods.** Each `send*` builds a small JSON `BotAction` (for example `{type: 'walkTo', x, z, running, reason: 'SDK'}`) and passes it to a private `sendAction`. ([rs:sdk/index.ts][index] L1124-1151)
  - They are **not strictly one per opcode**. They map to client-side operations, which the browser client's `ActionExecutor` turns into client calls. ([rs:ActionExecutor.ts][exec] L41-80)
  - Some methods choose between packet families. `sendUseItem` sends OPHELD1-5 for the main inventory and INV_BUTTON1-5 for other components. `sendClickComponent` uses IF_BUTTON and `sendClickComponentWithOption` uses INV_BUTTON. `sendSpellOnNpc` sends OPNPCT. ([rs:sdk/API.md][api] L258-306)
  - A few `send*` methods are not packets at all: `sendWait`, `sendScreenshot`, and `sendFindPath`, which is an alias for local pathfinding. ([rs:sdk/API.md][api] L297-312)
- **Rest of `BotSDK`.** It also holds:
  - synchronous state queries (`get*`, `find*`), where name patterns pick the shortest matching name, then the nearest, and reachable targets are preferred;
  - waits (`waitForCondition`, `waitForTicks`, `waitForStateUpdate`, `waitForChat`);
  - on-demand scans (`scanNearbyLocs`);
  - connection management.

  ([rs:sdk/API.md][api] L156-256)
- **Typing.** Everything is TypeScript.
  - Every `send*` returns `Promise<ActionResult>`, where `ActionResult` is `{ success; message; data?; reason?; phase? }`. `phase` is one of `'validation' | 'routing' | 'dispatch' | 'observation' | 'completion'`. The doc comments say low-level actions report `dispatch` and porcelain actions may report observation or completion. ([rs:sdk/API.md][api] L580-593)
  - Porcelain methods return per-method interfaces such as `PickupResult` or `MineRockResult`. These carry typed `reason` unions and extra fields. ([rs:sdk/API.md][api] L595-930)
- **Documentation.** Each public method has a JSDoc comment, and those comments are rendered into the generated `sdk/API.md` (see §8).
  - `CLAUDE.md` adds hand-written quick-reference tables for about 30 `bot.*` methods and the commonly used `sdk.*` methods. ([rs:CLAUDE.md][claude] L185-247)
  - The generated doc begins with an "Execution model" preamble. It states the dispatch-versus-effect rule and two easy mistakes: slot versus array index, and unawaited promises. ([rs:sdk/API.md][api] L6-11)

### What a low-level success means

- `sendAction` works as follows ([rs:sdk/index.ts][index] L1073-1121, L2026-2050):
  1. It creates an `actionId` and sends `{type:'sdk_action', username, actionId, action, actionTimeoutMs}`.
  2. It waits for the `sdk_action_result` with the same id.
  3. A dispatch timeout, a dropped socket or a gateway `sdk_error` becomes `{success:false, reason:'timeout'|'disconnected'|'error', phase:'dispatch'}` instead of an exception. A comment explains that one dropped action should not kill the caller's script ([rs:sdk/index.ts][index] L140-156).
  4. It does throw if the SDK is not connected at all.
- **Default dispatch timeout.** The code defaults `actionTimeout` to 60000 ms ([rs:sdk/index.ts][index] L218). The SDK README table says 30000 ([rs:sdk/README.md][sdk-readme] L160). This is a doc/code mismatch.
- **Meaning of success.** The top README and API.md both say a successful `send*` only proves the client accepted and dispatched the command. It does not prove the server applied the effect. ([rs:README.md][readme] L65-68, L95-98; [rs:sdk/API.md][api] L9)
  - The older SDK README still says Plumbing actions resolve on game **acknowledgment** ([rs:sdk/README.md][sdk-readme] L193). That wording conflicts with the newer docs.
- **Failures in the client before anything is sent** (reported with `phase: 'validation'` or `'routing'`):
  - `client_rejected`
  - `out_of_range`: the target is outside the loaded build area.
  - `cant_reach`: the client's pathfinder found no route, so no packet was written and the server never replies.

  ([rs:ActionExecutor.ts][exec] L50-60, L425-473; [rs:CLAUDE.md][claude] L354-358)
- **Silent server rejections.** When the engine refuses an op, it sends only `UNSET_MAP_FLAG`. The client counts these into `state.opFeedback.opRejectedCount`. The docs call this a strong hint rather than proof, because a normal walk ending also unsets the flag. Agents are told to snapshot the counter before a send and compare afterwards. ([rs:sdk/API.md][api] L515-530; [rs:lite/README.md][lite] L130-138)

### How a high-level call decides it succeeded

The implementations follow the same steps:

1. **Clear the screen.** Call `dismissBlockingUI()` first, which closes level-up dialogs and similar. ([rs:CLAUDE.md][claude] L290-302)
2. **Resolve the target.** Accept an entity, a string or a RegExp. A missing target returns a typed reason such as `loc_not_found` or `item_not_found`.
3. **Walk.** If the target is more than about 2 tiles away, call `walkTo`, which pathfinds and opens doors. A failed walk becomes `reason: 'cant_reach'`.
   - Many methods are wrapped in `withDoorRetry(..., r => r.reason === 'cant_reach', 2, ...)`. ([rs:sdk/actions.ts][actions] L871-879, L4095-4110)
   - After walking, `interactLoc` re-finds the target, because the loc may have changed. ([rs:sdk/actions.ts][actions] L4140-4165)
4. **Baseline before dispatch.** Record counts, XP and the latest game-message tick *before* sending. A comment explains why: the dispatch round trip can outlast a tick, so a baseline taken afterwards can already contain the result. ([rs:sdk/actions.ts][actions] L905-912)
5. **Dispatch** with a `send*` call. If dispatch fails, return its message.
6. **Wait on a predicate** with `sdk.waitForCondition(state => ..., timeoutMs)`. Each new state snapshot is checked against:
   - a success signal, such as the inventory count of the exact item id going up, or XP going up; or
   - a terminal failure signal, such as the target disappearing from `nearbyLocs`/`groundItems`, or a game message after the baseline containing text like "can't reach" or "inventory ... full".
7. **Classify the result.** For example, `chopTree` only reports success when logs were actually *gained*. If the tree despawned and no logs arrived, it reports that someone else felled it. A comment records that treating the despawn as success once produced false successes. ([rs:sdk/actions.ts][actions] L699-736)

Timeouts and idle detection seen in the code:

| Method | Behaviour |
|---|---|
| `chopTree` | waits up to 30 s ([rs:sdk/actions.ts][actions] L720-724) |
| `pickupItem` | waits up to 10 s, plus 1.5 s of grace for the inventory add after the ground item vanishes (L924-958) |
| `walkTo` | bounds the whole route, door attempts included, with `withExecutionDeadline` (default 120 s). It refuses to start on state older than 10 s (`reason: 'stale_state'`). A deadline overrun becomes `reason: 'timeout'` (L1068-1100) |
| `interactLoc` / `interactNpc` | succeed on an "effect" (animation, dialog or interface), so success can mean the action *started*. They fail when the player has been idle for 2 ticks with nothing happening ([rs:sdk/API.md][api] L68-69) |
| pickpocketing | gives up after 3 idle ticks without the expected attempt message ([rs:sdk/actions.ts][actions] L100-106) |
| `attack` | default 5000 ms; `castSpell` default 3000 ms ([rs:sdk/API.md][api] L116-118) |
| `waitForCondition` | *throws* `waitForCondition timed out` (default 30 s), so porcelain methods catch it and return `success:false` ([rs:sdk/API.md][api] L233) |

Other result shapes:

- Results can be partial. `buyFromShop` returns `success:false, partial:true, amountBought` on a short fill. ([rs:sdk/API.md][api] L84)
- Reason unions are per method, for example `PickupResult.reason` is `'item_not_found' | 'cant_reach' | 'inventory_full' | 'taken_by_other' | 'timeout'`. ([rs:sdk/API.md][api] L630-639)
- `castSpell` counts a splash as success with `hit:false`, because Magic XP is the evidence used. ([rs:sdk/API.md][api] L118)

### Representative signatures (descriptions in my own words)

- `async sendInteractLoc(x: number, z: number, locId: number, option: number = 1): Promise<ActionResult>`
  Asks the client to click option N on a world object at a tile. Success means the client sent the click, nothing more. ([rs:sdk/API.md][api] L263)
- `async chopTree(target?: NearbyLoc | string | RegExp): Promise<ChopTreeResult>`
  Finds a tree (by default the nearest name matching `^tree$`) and clicks "chop". It then waits until log count rises or the tree vanishes, and reports success only on a gain. ([rs:sdk/actions.ts][actions] L699-736)
- `async walkTo(x: number, z: number, tolerance: number = 3, options: { timeout?: number } = {}): Promise<ActionResult>`
  Uses local collision data to route to a tile in legs, opening doors on the way. It stops on cancellation, death, stale state or the overall deadline. ([rs:sdk/API.md][api] L76)
- `async interactLoc(target, option: number | string | RegExp = 1): Promise<InteractLocResult>`
  A generic "walk to the object and use option X". It resolves the option by visible text and waits for some visible effect, or fails after 2 idle ticks. ([rs:sdk/API.md][api] L68)

---

## 2. State

- **Shape.** `BotWorldState` is one object per publication ([rs:sdk/API.md][api] L532-578; [rs:StateCollector.ts][collector] L164-196). It contains:
  - `tick`, a monotonic `revision`, `inGame`;
  - `player`: name, combat level, hp/maxHp, tile and world coordinates, plane, run energy and weight, `animId`, `spotanimId`, `combat` target, and death/respawn counters (`isDead`, `lifeId`, `respawnCount`);
  - `skills`, `inventory`, `equipment`;
  - `nearbyNpcs`, `nearbyPlayers`, `nearbyLocs`, `groundItems`;
  - `gameMessages`, `recentDialogs`, `dialog`, `interface`, `shop`, `bank`, `trade`, `ge`, `modalOpen`/`modalInterface`;
  - `combatStyle`, `combatEvents`, `prayers`, `isSkulled`, `autoRetaliateEnabled`, `opFeedback`.

  Optional fields are marked absent when the connected client is too old to produce them.
- **Where it comes from.** `BotStateCollector.collectState()` reads the game client's own in-memory model, then derives a few things:
  - a per-tick reachability flood fill (`ReachProbe`) that sets `reachable` on targets;
  - `opFeedback` counters built from `UNSET_MAP_FLAG`;
  - combat events and chat observation ids.

  ([rs:StateCollector.ts][collector] L1-2, L124-196)
  - The headless `LiteClient` is a façade with the same fields and methods as the browser `Client`. The *same* `StateCollector` and `ActionExecutor` therefore run on it unmodified. ([rs:lite/README.md][lite] L21-33)
- **How it is refreshed (push, not pull).**
  - The client publishes once per game tick and again straight after each action result. ([rs:state-fanout.ts][fanout] L3-7)
  - The gateway caches the last state and drops a same-tick frame that differs only in `revision` ([rs:state-fanout.ts][fanout] L13-16). It replays the cached state to an SDK that has just connected ([rs:gateway.ts][gateway] L596-603).
  - The SDK keeps only the latest snapshot. `getState()` is synchronous. `onStateUpdate` listeners and the `waitFor*` helpers sit on top of it.
- **Freshness.**
  - `getStateAge()` and `stateReceivedAt` (stamped by the gateway) expose how old the snapshot is.
  - `runScript` requires the `tick` to advance within 10 s before it treats the client as live. A comment notes that `getStateAge()` mixes the gateway's clock with the local one. ([rs:sdk/runner.ts][runner] L215-254)
  - API.md says updates arrive about every 300 ms ([rs:sdk/API.md][api] L236). The README gives the default tick as 400 ms ([rs:README.md][readme] L109). The docs disagree.
- **What is limited or excluded.**
  - **Radius.** `nearbyLocs` and `groundItems` cover a 15-tile radius by default. `scanNearbyLocs(radius)` and `scanGroundItems(radius)` are on-demand requests, described as cheaper than pushing more data on every tick. ([rs:StateCollector.ts][collector] L1087-1088, L1274-1275; [rs:sdk/API.md][api] L249-256)
  - **Chat window.** Each sync carries at most about 50 messages, and the client ring holds 100. The SDK accumulates a 500-deep history per SDK instance from the moment it connects. ([rs:sdk/index.ts][index] L176-181; [rs:learnings/chat.md][l-chat] L1-15, L55-61)
  - **Bank and GE.** Bank contents only exist while the bank is open. GE state only exists at an open exchange. ([rs:sdk/API.md][api] L185-188, L244)
  - **Menu actions.** `menuActions` is published by the browser client but the SDK never reads it. It is the one field derived from rendering. ([rs:lite/README.md][lite] L95-96)
  - **Player chat** is removed from state when `showChat` is false (see §7).
- **Is it limited to what a real client knows?**
  - **Mostly yes, by construction.** All world state is read out of a real client's memory.
  - **Exceptions found:**
    - The SDK's `findPath`/`walkTo` uses a tracked whole-world `sdk/collision-data.json` (about 23 MB) built from the server's map data ([rs:DEVELOPERS.md][dev] L113-124).
    - The lite client builds a whole-world loc index from `/ondemand.zip` ([rs:lite/README.md][lite] L46).

    This is game cache data a client can download, but it is more than a live client has loaded at any moment.
  - **Not stated in the source.** I found no explicit fairness principle in the files I read. The closest is the integration-test "No Cheating" rule: never change game state through the browser or the engine/database, and do every game action through the SDK. ([rs:sdk/test/PRINCIPLES.md][principles] L95-99)
  - **Server changes.** The server itself is modified for bots: faster XP, infinite run energy, no random events. ([rs:README.md][readme] L82-88)

---

## 3. Architecture

- **Processes.** You run three yourself, and the SDK is the fourth ([rs:README.md][readme] L91-128):
  - the game engine, default tick 400 ms, configurable with `NODE_TICKRATE` or `::speed`;
  - the web client bundler or the lite runner;
  - the gateway, a Bun WebSocket server on `ws://localhost:7780`;
  - the SDK, inside a script or the MCP server.
- **Routing.** The gateway "accepts connections from botclient and SDK instances, and forwards messages between them based on username" ([rs:README.md][readme] L95).
  - There is one bot session per username and many SDK sessions per bot.
  - The SDK session id is generated by the server. The client-supplied id is never trusted as identity, to prevent cross-account injection, and `message.username` on actions is ignored. ([rs:gateway.ts][gateway] L207-241, L622-625)
- **Control modes** ([rs:types.ts][types] L40-44; [rs:gateway.ts][gateway] L566-583, L610-619):
  - `control`: the **last controller wins**. A new controller pre-empts the old one, which gets `sdk_error: 'Disconnected: another controller connected'`, and the old SDK then disables auto-reconnect.
  - `observe`: read-only, except that it may send `say`/`privateMessage`. It never pre-empts and is never pre-empted.
- **Message protocol.** JSON over WebSocket, typed in `server/gateway/types.ts` ([rs:types.ts][types] L12-74):

  | Direction | Message types | Payload |
  |---|---|---|
  | Client → Gateway | `connected`, `state`, `actionResult`, `screenshot_response`, `setGoal` | `actionId` echoed for correlation; `maxMessageLength` on `connected` |
  | Gateway → Client | `action`, `screenshot_request`, `save_and_disconnect`, `status`, `error`, `thinking` | `actionTimeoutMs` lets the client expire queued work before the SDK gives up |
  | SDK → Gateway | `sdk_connect`, `sdk_action`, `sdk_screenshot_request` | `sdk_connect` carries username, password and mode |
  | Gateway → SDK | `sdk_connected`, `sdk_state`, `sdk_action_result`, `sdk_error`, `sdk_screenshot_response`, `sdk_info` | `sdk_state` carries `state` and `stateReceivedAt` |

- **Client-side execution.** The browser client runs actions through a single-consumer FIFO: one action at a time, at most 64 pending, 55 s time-to-live. Entries that can no longer finish before the SDK deadline are expired. ([rs:ActionQueue.ts][queue] L11-55)
- **HTTP side-doors on the gateway.** `GET /status`, `/status/:username`, `/chat/:username`, `/state/:username`, `POST /say/:username` and `POST /dm/:username`. Chat sent this way is relayed through the bot's client. ([rs:gateway.ts][gateway] L995-1004, L1170-1176)
- **Auth and deployment.**
  - With `LOGIN_SERVER` enabled, SDK connections are authenticated per bot. With it off, any password is accepted, so a private fleet gateway should bind to 127.0.0.1. ([rs:gateway.ts][gateway] L52-55; [rs:sdk/README.md][sdk-readme] L172-187)
  - On the demo host, a direct gateway port with permessage-deflate (about 6× smaller frames) is tried first. The fallback is the `/gateway` relay through the game server. ([rs:sdk/README.md][sdk-readme] L163-170)
- **Why the SDK must go through a client.**
  - **What the README says.** The SDK "can't talk directly to the game server, but must go through a client" ([rs:README.md][readme] L100).
  - **Reasons given in the lite-client README** ([rs:lite/README.md][lite] L21-33, L54-65):
    - The browser client "is the specification" and serves as an *oracle*.
    - The lite client stays compatible by running the browser's own state-collector and action-executor code. The movement code is a verbatim port of the client's `tryMove`, and a test checks that its collision flags are byte-identical.
    - The client pathfinds before every interaction ([rs:CLAUDE.md][claude] L357).
  - **What the source does not say.** I found no explicit statement that this was chosen for *fairness*. The stated benefits are reuse of a correct client and the same game rules. I also did not find the original design discussion.

---

## 4. Entry points for the LLM

### MCP `execute_code`

- **Setup.**
  - The MCP server is `mcp/server.ts` over stdio. It is pre-registered for Claude Code (`.mcp.json`), opencode (`opencode.json`) and Codex (`.codex/config.toml`).
  - Tools: `execute_code`, `list_bots`, `disconnect_bot` and `get_market`.
  - It exposes `sdk/API.md` and `sdk/MARKET.md` as MCP resources.

  ([rs:mcp/README.md][mcp-readme] L28-99; [rs:mcp/server.ts][mcp-server] L42-52)
- **Parameters.** `bot_name`, `code`, and `timeout` in minutes (default 2, clamped to 0.1-60). ([rs:mcp/server.ts][mcp-server] L92-108, L179-180)
- **Connection.** The first call auto-connects in `control` mode, using `bots/{name}/bot.env` for credentials, the gateway URL and `SHOW_CHAT`. It may launch a browser client if the session is stale. ([rs:mcp/api/index.ts][mcp-api] L52-103)
- **Execution.**
  - The snippet is transpiled with `Bun.Transpiler` and wrapped as `async (bot, sdk, Spells) => { ... }`, then called through `new Function`. ([rs:mcp/execute-code.ts][mcp-exec] L105-109)
  - Before running, it waits briefly for fresh in-game state. If none arrives, it **runs anyway** and adds a warning, because a stale client is exactly when an agent needs to poke around. (L94-101)
- **Tool output** (L125-148):
  1. **Console:** bounded to the first 300 and last 300 lines, 2000 characters per line ([rs:console-capture.ts][console] L47-49).
  2. **Result** (JSON with credentials redacted), *or* **Error** (the first 7 lines, elapsed time, and which `bot.`/`sdk.` call was in flight).
  3. **World State:** a brief summary, appended to every call. It includes only messages newer than the last call's.
  4. **Tip:** if the code is longer than 2000 characters, a suggestion to move it into a saved script.

  The result sets `isError` on failure.
- **Long runs and crashes.**
  - **One execution per bot.** A new call aborts the running one, waits for it to unwind, then starts. ([rs:mcp/execute-code.ts][mcp-exec] L8-34)
  - **Aborts.** Timeout, MCP cancellation or a newer call abort an `AbortController`. That controller is carried in an `AsyncLocalStorage` execution context which every SDK wait and dispatch checks. ([rs:sdk/execution-context.ts][ctx] L1-73)
  - **After an abort**, every proxied `bot`/`sdk` call returns a promise that never resolves, instead of throwing. Otherwise a catch-all loop in the cancelled code could spin and starve the server. ([rs:mcp/execute-code.ts][mcp-exec] L39-58)
  - **Leftover async work** started by the snippet is aborted when the run ends (L121-122).
  - **Packets already sent may still take effect.** ([rs:mcp/README.md][mcp-readme] L57-61)

### Standalone `runScript`

- **Usage.** Files live under `bots/<name>/` and call `runScript(async ({ bot, sdk }) => {...}, options)`. Credentials come from `bot.env` next to the script, or from a bot-name argument. ([rs:sdk/runner.ts][runner] L96-131; [rs:bots/_template/script.ts][tpl-script])
- **Options** ([rs:sdk/runner.ts][runner] L30-58):
  - `timeout` (no default);
  - `autoConnect`;
  - `disconnectAfter`;
  - `printState`;
  - `onDisconnect`: `'error'` (default, throws `BotDisconnectedError`), `'wait'` or `'ignore'`;
  - `reconnectTimeout`.
- **No client, no script.** It **never starts a game client**. If no client tick arrives within 10 s, it fails with a message explaining how to start one. ([rs:sdk/runner.ts][runner] L215-254; [rs:README.md][readme] L50-61)
- **Shutdown.** SIGINT aborts the script and exits with 130. ([rs:sdk/runner.ts][runner] L363-380)
- **Output and exit code.** It prints Result, or Error with 4 stack frames, then a State summary that includes XP gained. On failure it sets `process.exitCode = 1`. It returns `{success, result, error, duration, finalState}`. (L440-499)
- **Headless clients.** The lite runner (`bun src/lite/runner.ts <name>`) runs one process per bot and exits non-zero when the game session ends. Sessions end with a typed reason (`stopped`, `logged-out`, `disconnected`, `idle`, `error`), so a supervisor can tell "finished" from "died". ([rs:CLAUDE.md][claude] L32-45; [rs:lite/README.md][lite] L111-117)

### How the LLM checks in

- **Workflow taught in `CLAUDE.md`** ([rs:CLAUDE.md][claude] L100-174):
  1. dump state with `bun sdk/cli.ts {username}`;
  2. write a focused script;
  3. run it;
  4. observe and re-dump;
  5. record notes in `lab_log.md`.
- **Run length.** "Start short": 10 s for new or untested logic, 30 s to 1 min once validated, 5+ min "sparingly". Agents are told to grep `learnings/` and `wiki/`.
- **Chat.** `bun sdk/chat.ts` talks or reads in `observe` mode without taking control from the running script. ([rs:CLAUDE.md][claude] L138-158)
- **One owner per bot.** Only one control connection should own a bot. Agents should check `sdk.getStateAge()` after a pause. ([rs:CLAUDE.md][claude] L256-258)
- **RuneBench prompts.** They tell agents to background long scripts (`bun /tmp/x.ts > /tmp/log 2>&1 &`), `tail` the log and poll a rate checker. They also warn against the `timeout` command, because its SIGTERM kills the game connection. ([rb:generate-tasks.ts][rb-gen] L186-193)

---

## 5. Feedback loop

- **The tool.** `bun sdk/bug-report.ts "<text>"` is a **CLI**, not an SDK method callable from `bot`/`sdk` code. ([rs:sdk/bug-report.ts][bug-cli])
  - It POSTs `{text, sdkVersion}` to the hard-coded `https://rs-sdk-demo.fly.dev/bug-report`, where `sdkVersion` is `git rev-parse --short HEAD`.
  - It always goes to that endpoint, whatever server the bot plays on: a bug found on localhost is still a bug in the SDK.
  - The request times out after 15 s. Setting `TELEMETRY` to `false`, `0`, `no` or `off` disables it (L17-81).
  - The bot.env template ships `TELEMETRY=true` ([rs:bots/_template/bot.env][tpl-env]).
- **The server side** is a write-only drop box ([rs:server/engine/src/web/pages/bug-report.ts][bug-srv] L4-23, L77-151):
  - **Storage.** Each record `{id, receivedAt, text, sdkVersion}` is appended to a JSONL file: `BUG_REPORTS_FILE`, or `data/bug-reports.jsonl`. It guards against a truncated last line.
  - **Limits.** Bodies up to 64 KB; text cut at 8000 characters; the log stops accepting writes at 32 MB (HTTP 507).
  - **Access.** No auth. Reports can't be read back over HTTP: anything except POST gets 405, and maintainers read the file over `fly ssh`.
  - **Body formats.** A plain-text body is accepted too.
- **Agent instructions** ([rs:CLAUDE.md][claude] L87-98):
  1. Find a workaround *first*.
  2. Then file the report.
  3. Assume the game is right and question your own assumptions.
  4. Keep working on the goal.
- **Other feedback channels.**
  - The `lab_log.md` template has a "Possible SDK Bugs or Improvements" section. ([rs:bots/_template/lab_log.md][tpl-log])
  - The RuneBench site says the SDK was grown through cycles of automated error analysis: run a batch of agents, categorise failures and missing features, improve the harness ("Harness Development → Growing an API").
  - The RuneBench repo has a `scripts/extract-sdk-audit.ts`. I only saw its name and did not read it.

---

## 6. Game knowledge for agents

- **Top-level instructions.** There is one file, `CLAUDE.md`. `AGENTS.md` and `GEMINI.md` are git symlinks to it (tree mode `120000`). ([rs:CLAUDE.md][claude])
  - **Sections:** first-time setup, client start-up, tools, bug reporting, session workflow, chat, script-duration table, API quick reference, code environments, gotchas (modals hide the inventory, dialog option order, level-up dialogs), project layout and troubleshooting.
  - **Style:** second person, task-oriented. It names concrete failure symptoms ("Everything succeeds and nothing happens") and gives their causes.
- **`learnings/`** ([rs:learnings/README.md][l-readme] L1-43):
  - **Topics:** 15 files (banking, chat, combat, cooking, dialogs, draynor-manor, fishing, fletching, jewelry-crafting, mining, shops, smithing, thieving, walking, woodcutting) plus a README.
  - **Content:** working patterns, coordinates tables and gotchas.
  - **Conventions:**
    - Snippets are written for the `execute_code` environment, with `bot`/`sdk` as globals and top-level `await`.
    - The README explains the `runScript` wrapper for standalone files and restates the action semantics.
  - **Enforcement:** a unit test checks every learnings snippet. It bans `ctx.bot`, `ctx.sdk`, `state.combat` and typed function declarations, and requires every `ts`/`typescript` fence to parse. ([rs:sdk/test/docs-snippets.test.ts][snippets] L10-52)
- **`wiki/`** (around 2,580 files):
  - **Generated:** `items/` (1582), `npcs/` (807) and `shops/` (119), produced by `wiki/generate-{items,npcs,shops}.ts` from the server's content files ([rs:wiki/generate-items.ts][wiki-gen] L1-15).
  - **Not generated:** `skills/` (18) and `quests/` (59) have no generator in the repo, so they appear hand-written. The source does not say.
  - **Format:** markdown detail tables, with drop sources cross-linked to NPC pages.
- **`docs/`.** Maintainer material: a Grand Exchange maintenance note, a server-performance JSON and badge images. Nothing in it is addressed to agents, as far as I can see.
- **Other agent-facing docs:**
  - `sdk/API.md` (generated);
  - `sdk/MARKET.md`;
  - `mcp/README.md`;
  - per-bot `bots/<name>/{bot.env, lab_log.md, script.ts}`, created from `bots/_template` by `bots/create-bot.ts` ([rs:CLAUDE.md][claude] L6-30).
- **RuneBench note.** RuneBench copies its own `agents.md` to `CLAUDE.md`. A comment says Claude Code only auto-loads `CLAUDE.md`/`AGENTS.md`, so guidance in a lowercase `agents.md`, including "grep learnings/ and wiki/", never reached the agent. ([rb:docker/Dockerfile][rb-docker] L44-49)

---

## 7. Safety

- **Chat as untrusted input.**
  - **Warning.** The README warns that seeing other players' chat exposes the bot to scamming and prompt injection. It offers `SHOW_CHAT=false` in `bot.env` or `create-bot --no-chat` as the opt-out. ([rs:README.md][readme] L75)
  - **Default.** Chat is on by default in the SDK constructor, `runScript` and the MCP manager ([rs:sdk/index.ts][index] L223; [rs:sdk/runner.ts][runner] L141-142; [rs:mcp/api/index.ts][mcp-api] L79-83).
  - **Stale comment.** The `showChat` doc comment in `types.ts` still says the default is false "for safety" ([rs:sdk/types.ts][types-sdk] L729).
  - **The filter** runs **in the SDK**. When `showChat === false`, it drops player-chat types 1, 2, 3, 6 and 7 from each `sdk_state` *before* chat history is recorded, so system messages still pass. ([rs:sdk/index.ts][index] L1958-1968)
    - The client still holds the chat, and so does the gateway (`/chat/:username`).
    - Hiding chat also hides DMs, which breaks bot-to-bot coordination ([rs:learnings/chat.md][l-chat] L60-61).
  - **No other defence found.** When chat is on, player lines go into the `execute_code` world-state summary verbatim, under a separate "Player Chat" heading. They are not quoted or marked untrusted. ([rs:sdk/formatter.ts][fmt] L444-466)
- **No sandbox.**
  - `execute_code` evaluates agent code with `new Function` *inside the MCP server process*, with full Bun/Node access ([rs:mcp/execute-code.ts][mcp-exec] L105-109).
  - `runScript` files are ordinary `bun` programs.
  - The only runtime containment is cooperative cancellation (§4).
- **Other safety-relevant mechanisms:**
  - controller pre-emption, plus observe mode limited to chat;
  - server-side session ids against cross-account injection ([rs:gateway.ts][gateway] L216-221);
  - credentials redacted from tool output ([rs:sdk/redact.ts][redact]);
  - a demo-server request: please don't play manually ([rs:README.md][readme] L77).
- **RuneBench isolation** is at the container level ([rb:generate-tasks.ts][rb-gen] L195, L282-288; [rb:docker/Dockerfile][rb-docker] L192-197):
  - one Docker environment per task;
  - the agent runs as an unprivileged `agent` user;
  - the engine, saves, SDK, tracker and tracker output are root-owned and read-only, so the agent can't edit the data it is scored on;
  - the verifier kills agent-spawned `bun` processes before measuring (L135-148).

---

## 8. API reference generation

- **What `sdk/generate-api-docs.ts` does.** It parses five files with the TypeScript compiler API: `sdk/actions.ts`, `sdk/index.ts`, `sdk/types.ts`, `sdk/ge-types.d.ts` and `sdk/market.ts`. ([rs:sdk/generate-api-docs.ts][gen] L1-8, L234-253)
  - **Methods.** It takes every *public* (non-private, non-protected) method of the `BotActions` and `BotSDK` classes. Signatures are normalised to keep the `async` marker, parameters with defaults, and the return type. The last JSDoc comment becomes the description, or "_No description provided._". (L37-102)
  - **Types.** It takes every exported interface whose name ends in `Result` or `State`, or starts with `GE` or `Market`, together with property doc comments. (L104-125)
  - **Grouping.** Methods are sorted into categories by keywords in their names: Movement, Banking, Raw Actions, State Access and so on. (L133-169)
  - **Preamble.** A fixed "Execution model" preamble comes first. (L194-216)
- **Commands.**
  - `bun run docs:api` writes the file.
  - `bun run docs:api:check` throws `sdk/API.md is stale. Run bun run docs:api and commit the result.` when the output differs. ([rs:package.json][pkg]; [gen] L259-287)
- **The CI check.** `.github/workflows/ci.yml` runs on every PR and every push to main. Its last step, "Type-check, test, and verify generated docs", runs `bun run check`, which is two type-checks plus `bun run test`. ([rs:ci.yml][ci] L1-39; [rs:package.json][pkg])
  - The docs are verified by a **unit test**, `sdk/test/api-docs.test.ts`, not by calling `docs:api:check`. ([rs:sdk/test/api-docs.test.ts][api-test] L13-53) It asserts that:
    - the checked-in `API.md` equals freshly generated output;
    - there are more than 100 methods, all with typed signatures;
    - previously omitted methods (`navigateDialog`, `onStateUpdate`, `waitForCondition`, ...) are present;
    - specific signatures keep their defaults and `async` markers.
  - The same test run also checks the learnings snippets (§6).

---

## 9. RuneBench

- **Location.** The benchmark code is **not in rs-sdk**. The only "bench" files in rs-sdk are lite-client performance benchmarks. RuneBench is a separate repo that clones rs-sdk into a Docker image. ([rb:CLAUDE.md][rb-claude] L1-4)
- **Harness: [Harbor](https://harborframework.com/).**
  - `generate-tasks.ts` writes one directory per task, containing `task.toml`, `instruction.md`, `tests/test.sh` with a copied verifier, and `environment/Dockerfile` with a starting `agent.sav`.
  - The Dockerfile is a thin `FROM ghcr.io/maxbittker/rs-agent-benchmark:v71`. ([rb:generate-tasks.ts][rb-gen] L1-19, L315-357)
  - Runs use `harbor run --env modal -k <trials>` with per-model agent adapters (`agents/*_adapter.py`). ([rb:scripts/run.sh][rb-run] L109-117)
  - `dataset.toml` publishes the tasks as `maxbittker/runebench`. ([rb:dataset.toml][rb-ds])
- **Task format** ([rb:generate-tasks.ts][rb-gen] L197-217, L268-313):

  | Setting | Value |
  |---|---|
  | `[agent] timeout_sec` | duration + 120 s |
  | `user` | `agent` |
  | `[verifier] timeout_sec` | 400 |
  | Environment | 2 CPUs, 8192 MB memory cap, 10 GB storage, internet allowed |
  | MCP server | `rs-agent` over stdio (`bun run /app/mcp/server.ts`) |

  - **Task set:** 16 skills × {15 min, 30 min}, a 5-minute woodcutting smoke task, and 4 gold starting conditions × {15 min, 30 min}: vanilla, smith-alch, fish and fletch-alch, each with its own save. (L30-47, L108-133, L219-264)
  - **Inconsistency:** the file header mentions a 2 h gold task, but the code defines only 15 and 30 min.
- **Instructions.** Agents get prose `instruction.md` prompts. The skill prompt ([rb:generate-tasks.ts][rb-gen] L157-195):
  - explains the score and points to `execute_code` with `bot_name: "agent"`;
  - says to read the MCP API resources, and not to invent skill-specific methods;
  - says to start with ONE action (`bot.skipTutorial()`) and get a non-zero baseline before planning;
  - says to background long scripts;
  - forbids cheating: no editing server or save files, no cheat commands.
- **Game speed.** The image sets `NODE_TICKRATE=50`, which is 8× the 400 ms default ([rb:docker/Dockerfile][rb-docker] L5). The server also has a 25× XP multiplier, so scores divide raw XP by 8 × 25 = 200 ([rb:generate-tasks.ts][rb-gen] L161).
- **The peak-window metric** (skill tasks):
  - **Sampling.** A root-owned tracker connects in `observe` mode. Every 15 s it samples all skills, plus bank and inventory gold read from the save, into `/logs/tracking/skill_tracking.json`. ([rb:shared/skill_tracker.ts][rb-tracker] L1-24, L122-130)
  - **Peak computation.** The verifier walks the samples. For each sample it finds the nearest earlier sample at least 12 s back (`MIN_PEAK_WINDOW_MS`), so a restart cannot turn a 1-second spike into the peak. It computes `ΔXP/Δms × 60000 / 8 / 25` and keeps the maximum, rounded. ([rb:shared/check_skill_xp.ts][rb-check] L28-73)
  - **Aligned windows.** Windows sit on the fixed 15 s cadence; they don't slide. ([rb:generate-tasks.ts][rb-gen] L161)
  - **Output.** The verifier writes `reward.txt` (the peak rate) and `reward.json` (with the tracking data). ([rb:shared/check_skill_xp.ts][rb-check] L123-140)
  - **Stale comment.** The file header says `reward.txt` holds raw XP, but the code writes the peak rate.
- **Why peak rate** (RuneBench website, "Task Design"):
  - The original score was total XP in a fixed window. It punished exploration, because the best strategy was a non-stop simple grind.
  - Max XP rate per 15-second window rewards agents that find higher-level strategies, not just time on task.
  - **Limitations the author lists:**
    - runs take a long time;
    - shorter tasks punished models that front-load planning by reading docs for minutes, and slow models;
    - the environment is complex and sample counts are low, so the results are noisy.
  - **Future work the author lists:** multi-agent collaboration (one LLM driving several characters, or separate LLMs talking through in-game chat) and knowledge transfer (one agent writing a guide for another).
- **Gold tasks.** Sampling is every 5 s. The reward is the *peak* of any sample's gold and the final save read (inventory + bank, read from the binary save file). ([rb:generate-tasks.ts][rb-gen] L229-236; [rb:shared/check_gold.ts][rb-gold] L1-12, L257-284)

---

## Implications for our Mind connection

> **Interpretation, not fact.** Everything in this section is my reading of how the findings above bear on our design. It maps rs-sdk onto `CONTEXT.md` and ADR 0001-0003 and should be grilled, not taken as settled.

### Mapping

| rs-sdk | Our term | Notes |
|---|---|---|
| `sdk.send*` (one per client interaction, returns a dispatch-level `ActionResult`) | **Primitive** | Close match. Theirs are shaped per client interaction rather than per opcode, which fits ADR 0002's "real client packet". |
| `BotWorldState` snapshot pushed per tick + `gameMessages`, `opFeedback`, `combatEvents` | **Percepts** (plus a Mind-side model built from them) | They push a *whole snapshot* built from the client's memory. ADR 0001 gives us a *stream of typed calls* from `IPacketLib`. Their Tactics depend heavily on a queryable current state (`waitForCondition(predicate)`, `find*`), so our Mind will probably need a world model built from Percepts. |
| `bot.*` porcelain methods, plus LLM-written `runScript` files and `execute_code` snippets | **Tactic** / **Tactic Library** | Their porcelain is human-written library code with a consistent recipe: clear UI → resolve target → walk → baseline → dispatch → wait for evidence or failure → typed reason. That recipe is a candidate shape for our built-in Tactics. |
| `lab_log.md`, bug-report JSONL, execute_code output, RuneBench tracking samples | **Journal** (partially) | rs-sdk has no single append-only record. These pieces are scattered and mostly agent-authored or for benchmarking. |
| gateway `control`/`observe`, last controller wins | **Possession**, single owner | Their pre-emption rule is a working answer to "who drives this character right now". |
| `learnings/`, `wiki/`, `CLAUDE.md` | **Lessons** / Planning context | Their learnings are human-curated and lint-checked. Ours are meant to come from Reflection. |

### Where we differ, and why it matters

- **We can give a stronger Primitive result than rs-sdk can.** Their client only knows that bytes went out. Our Harness is in-process (ADR 0002), so it could know that the packet handler ran on a given tick, or that the client tick never picked it up.
  - Anything beyond "the client sent it" may leak information a real client never gets. Silent server rejections are exactly that. This would break ADR 0001's fairness.
  - rs-sdk's compromise is to derive rejection only from a client-visible signal (`UNSET_MAP_FLAG` → `opFeedback`).
- **Their dispatch-versus-effect split is the most hard-won lesson visible in the source.** Three places say a sent op is not an accepted op: the README, the API preamble and the troubleshooting section. Porcelain code comments record false successes that came from weak evidence.
  - Our Primitive results and Tactic results should probably carry an explicit `phase` and a machine-readable `reason`, as theirs do.
- **Fairness is a design rule for us. For them it is mostly a side effect.** Their state is fair because it is read from a real client's memory. Even so, their SDK pathfinder uses a whole-world collision map.
  - ADR 0002 says the Harness must simulate movement. Where pathfinding lives, and which map knowledge it may use, needs deciding.
- **ADR 0003 holds up well.** rs-sdk is an out-of-process TypeScript SDK over WebSocket + JSON. MCP appears only as the outer layer: the LLM sends code through `execute_code`, and that code makes many fine-grained calls. This matches ADR 0003's rejection of MCP for Tactic → Primitive.
  - ADR 0003 claims TypeScript "can be sandboxed". rs-sdk does not sandbox: `new Function` runs in-process with full host access. We have to decide this ourselves.
- **Cancellation is a concrete pattern worth copying:**
  - one execution per Agent;
  - a new run aborts the old one and waits for it to unwind;
  - an `AbortSignal` in async context, checked by every wait and dispatch;
  - after an abort, calls *park* forever instead of throwing, so catch-all loops can't spin;
  - a hard bound on the whole route of a long Tactic.

  ADR 0003's goal of "bad code can't stall" covers the server. Inside the Mind it still needs exactly this.
- **Chat is a prompt-injection channel, and rs-sdk only hides it.** The Human, and later other Agents, will speak to our Agents. Our Percepts carry chat verbatim, by design, because a real client sees it.
  - The question is how the Mind presents chat to the LLM: quoted, attributed, marked untrusted.
- **Generated, CI-checked docs fit ADR 0003's codegen.** We plan to generate the TypeScript types for Primitives and Percepts from C#. A generated, test-enforced reference for the LLM, in the style of `API.md`, could come from the same source.
- **Snapshot fan-out costs.** rs-sdk pushes full JSON snapshots every tick, with deduplication and optional compression. Our Percept stream is incremental by construction. A model built in the Mind can still offer snapshot-style queries without sending snapshots over the wire.

### Open questions for grilling milestone 2

1. **Primitive result.** What does a Primitive result promise: "the packet was synthesized", "the handler processed it this tick", or nothing at all, with only Percepts as evidence? Which `phase`/`reason` values are allowed without breaking ADR 0001?
2. **Silent rejections.** When the server drops an op silently, do we expose a signal at all? If we do, is it only the client-visible one (for example a map-flag reset, if DAoC has an equivalent)?
3. **State shape.** Is the Mind's state model a per-tick snapshot, an event log, or both? Who builds it: generated code from Percept definitions, or hand-written reducers?
4. **Waits.** Which wait primitives do Tactics get: `waitForPercept(match, timeout)`, `waitForCondition(state => ...)`, waits in ticks? Does a timeout throw or return a result?
5. **Built-in Tactics.** Do they follow rs-sdk's recipe (baseline before dispatch, evidence-or-failure predicate, idle-tick failure detection, typed reasons)? Who writes the first set?
6. **Pathfinding and fairness.** Where does pathfinding live, Harness or Mind? What map and collision knowledge may it use without breaking fairness?
7. **Control.** Is there an equivalent of `control` versus `observe` (one driver, many watchers)? How does Possession pre-empt a running Tactic: abort, park, or pause?
8. **Cancellation.** What are the semantics when the LLM issues a new plan mid-Tactic: one execution per Agent, abort-and-unwind, parking calls?
9. **Sandboxing.** How are LLM-written Tactics sandboxed (isolate, worker, separate process, permissions)? Or do we accept rs-sdk's no-sandbox stance for now?
10. **LLM entry point.** Do we want an `execute_code`-style tool that returns console output, the result and a "new Percepts since last call" summary? What size limits apply?
11. **Chat.** How is chat (and any other player-authored text) marked as untrusted when shown to the LLM? Is there a per-Agent opt-out like `SHOW_CHAT`?
12. **Journal contents.** Should the Journal record every Primitive with its result `phase`/`reason` and every Tactic outcome, so the Human can find "everything succeeded and nothing happened" failures?
13. **Feedback loop.** Do Agents get a way to report Harness/Primitive papercuts, like `bug-report.ts`, separate from their Journal? Where does it go in a single-server setup?
14. **Agent docs.** Do we generate a Primitive/Percept reference from the C# definitions and enforce it in CI? Do we lint example Tactic snippets the way `docs-snippets.test.ts` does?
15. **Evaluation.** Should we have a RuneBench-style score (for example peak rate over aligned windows) to compare Mind versions? Do we need tracker data the Agent cannot write to?

<!-- Link references (pinned commits) -->
[readme]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/README.md
[claude]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/CLAUDE.md
[dev]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/DEVELOPERS.md
[pkg]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/package.json
[ci]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/.github/workflows/ci.yml
[sdk-readme]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/README.md
[api]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/API.md
[index]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/index.ts
[actions]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/actions.ts
[types-sdk]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/types.ts
[runner]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/runner.ts
[ctx]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/execution-context.ts
[fmt]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/formatter.ts
[redact]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/redact.ts
[bug-cli]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/bug-report.ts
[gen]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/generate-api-docs.ts
[api-test]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/test/api-docs.test.ts
[snippets]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/test/docs-snippets.test.ts
[principles]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/test/PRINCIPLES.md
[mcp-readme]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/mcp/README.md
[mcp-server]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/mcp/server.ts
[mcp-exec]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/mcp/execute-code.ts
[console]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/mcp/console-capture.ts
[mcp-api]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/mcp/api/index.ts
[gateway]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/gateway/gateway.ts
[types]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/gateway/types.ts
[fanout]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/gateway/state-fanout.ts
[collector]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/webclient/src/bot/StateCollector.ts
[exec]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/webclient/src/bot/ActionExecutor.ts
[queue]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/webclient/src/bot/ActionQueue.ts
[lite]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/webclient/src/lite/README.md
[bug-srv]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/engine/src/web/pages/bug-report.ts
[l-readme]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/learnings/README.md
[l-chat]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/learnings/chat.md
[wiki-gen]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/wiki/generate-items.ts
[tpl-script]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/bots/_template/script.ts
[tpl-env]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/bots/_template/bot.env
[tpl-log]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/bots/_template/lab_log.md
[rb-claude]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/CLAUDE.md
[rb-gen]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/generate-tasks.ts
[rb-run]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/scripts/run.sh
[rb-ds]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/dataset.toml
[rb-docker]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/docker/Dockerfile
[rb-tracker]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/shared/skill_tracker.ts
[rb-check]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/shared/check_skill_xp.ts
[rb-gold]: https://github.com/MaxBittker/runebench/blob/3e5ad08fc8ef3379e55e7262bb7ced14ddd1e5a8/shared/check_gold.ts
