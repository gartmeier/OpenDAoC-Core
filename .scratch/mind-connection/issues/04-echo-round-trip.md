# 04: Echo round trip: hear a say line, say it back

**What to build:** The Human types `/say hello` next to an Agent, and the Agent says `hello` back. This completes the round trip from the server to the Mind and back into the world.

On the server:
- The message call gets a hand-written Percept projection: its text, chat type and chat location, exactly what the 1.127 packet carries, with chat type and location as their enum names. The projection notes which packet-library method it mirrors (the one that actually applies to 1.127 in the override chain).
- A `say <text>` Primitive sends the packet from ticket 01. It's accepted only while the Agent is in the world; otherwise its `primitiveResult` says it wasn't queued, and why.

In the Mind:
- An echo module: given a Percept and the Agent's own name, it returns a `say` Primitive or nothing. It reads the speaker and the words from the say line's text, because the packet has no separate speaker field. It ignores the Agent's own lines and every other chat type.
- The entry point wires the echo module to the connection.

See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** 01, 02

**Status:** ready-for-human

- [ ] `/say hello` from the Human next to the Agent makes the Agent say `hello` back, once
- [ ] The Agent's own say line doesn't make it say anything
- [ ] Chat that isn't say, and system messages, don't make it say anything
- [x] Unit tests: the message call gives a Percept with exactly its text, chat type and location; a call without a projection still gives a bare Percept
- [x] A `say` sent while the Agent isn't in the world is refused with a reason
- [x] `bun test` covers the echo module: another speaker's say line, the Agent's own line, other chat types, unparsable text
- [x] The regenerated TypeScript protocol types include the message Percept and `say`, and the codegen test passes

## Comments

**2026-10-20 (agent):** Built on branch `claude/echo-round-trip-808789`. The round trip needs the Human with a running server and the real client, so this is back with the Human.

What exists now:
- **Message Percept.** `PerceptProjection` turns the message call (`SendMessage`) into a `message` Percept: `text`, `chatType` and `chatLocation`, with the enums as their names (`CT_Say`, `CL_ChatWindow`). It mirrors `PacketLib1127.SendMessage` and the `SendRawMessage` it calls, which override `PacketLib173`'s and `PacketLib168`'s. What that means:
  - The 1.127 packet carries the chat type, a marker for popups (`##`, never on a send line, which gets `@@` instead) and the text. So `chatLocation` is `CL_PopupWindow` or `CL_ChatWindow`: `CL_SystemWindow` isn't on the wire, and the client picks the window by chat type.
  - The text is what cp1252, the packet's encoding, carries: a character it can't encode comes out as `?`, as the client gets it.
  - A message of a chat type the client filtered out (the filter settings the Harness sends turn off `CT_OthersCombat` and `CT_SpellPulse`), or one at the character screen, isn't sent by the packet library, so it gives no Percept, as a property read gives none.
  - Every other call is still a bare Percept, including `SendRawMessage`, which only `/snoop` calls directly.
  - The projection now takes the Agent's client, for the filter and the state. `MindLink.Perceive` passes it on.
- **`say` Primitive.** `{"name":"say","text":"hello"}` queues `ClientPackets1127.Say(text)` from ticket 01, the `&s` command a real client sends for a chat line. Like `quit`, it's queued only while the Agent is in the world; otherwise its result says "<char> isn't in the world." The server's `/say` handler does the rest, its rules included: an empty text gets "You must say something...", and a second say within 0.5 s gets "Slow down!".
- **Protocol.** `mind/src/protocol.ts` is regenerated with `SayPrimitive`, `MessagePercept`, `eChatType` and `eChatLoc`. The generator now turns an enum into a union of its names, and the server writes enums as names.
- **How a say line reads.** From the server code: `GamePlayer.Say` sends nearby players (not the speaker) `GamePlayer.SayReceive.Says`, `{0} says, "{1}"` in the English language file, as `CT_Say` to `CL_ChatWindow`. `{0}` is `GetName(0, false)`: a Character's name as it is (always capitalized), an NPC's with "the" if its name is lowercase. `{1}` is the words as said. The speaker gets `You say, "{0}"`. A yell is `CT_Say` too but reads `{0} yells, "{1}"`, and a language the listener doesn't understand reads `{0} says something in a language you don't understand.`
- **Echo module** (`mind/src/echo.ts`). Given a Percept and the Agent's Character, it returns a `say` Primitive or nothing. It reads `<speaker> says, "<words>"` from a `CT_Say` message and says the words back, quotes inside them included. The Agent's own line (`You say, ...`), a line under its own name in any case (the Mind has the name as typed, `greeen`), other chat types, bare Percepts, yells, the foreign-language line, empty words and anything else that doesn't read that way give nothing.
- **Entry point.** `mind/agent.ts` hands each Percept to the echo module and sends the `say` it returns, with `Saying "hello".` on stderr, and `The say wasn't sent: <reason>` if it wasn't queued. It still prints every Percept; a message Percept now shows its chat type, location and text.
- Tests:
  - Server, 152 passing. `UT_PerceptProjection`: the message call's text, type and location; a popup; the system window as the chat window; a send line to a popup; the text as cp1252 carries it; a filtered chat type and the character screen give nothing; a bare Percept and a property read as before. `UT_MindProtocol`: reading a `say`, writing a message Percept with names. `UT_HarnessClient`: `say` and `quit` refused with a reason outside the world. `UT_MindProtocolTypeScript` passes on the regenerated file.
  - Mind, 13 passing in `bun test` (8 new for the echo module: another speaker, quotes in the words, an NPC, the Agent's own line, its name in another case, other chat types, bare Percepts, unparsable lines). `bun run typecheck` is clean.
  - Against a fake server, `bun mind/agent.ts greeen` answered `Vue says, "hello"` with one `say` of `hello`, and nothing for `You say, "hello"`, a system message or a bare Percept.

For the Human, when the server runs this build (the Mind setup from ticket 02 is unchanged):
1. `bun mind/agent.ts greeen`, and stand next to Greeen. Type `/say hello`. Greeen should say `hello` back once: your chat window shows `Greeen says, "hello"` once. The Mind prints your line as a `CT_Say CL_ChatWindow` Percept with the text `Vue says, "hello"`, then `Saying "hello".` on stderr.
2. Greeen's own line: the Mind should print `You say, "hello"` as a Percept and say nothing more, so there's exactly one reply per line you say. Try a line with quotes in it too, for example `/say she said "hi"`.
3. Other chat and system messages shouldn't make Greeen say anything: `/send greeen hello` (printed as `CT_Send`), `/yell hello`, an emote next to him such as `/wave`, and system messages such as the ones world entry and the quit countdown send.
4. A `say` while Greeen isn't in the world is refused. The echo Mind can't do that by itself, so save this as `mind/early-say.ts` (don't commit it), and run `bun mind/early-say.ts greeen` while Greeen is offline. It sends `say` on the first Percept, which arrives during world entry. It should print `{"type":"primitiveResult","id":1,"queued":false,"reason":"Greeen isn't in the world."}`, then Greeen enters the world without saying anything. Ctrl+C quits him.
   ```ts
   import { Connection } from "./src/connection.ts";
   import { loadEnvFile } from "./src/env.ts";

   await loadEnvFile(new URL(".env", import.meta.url));
   const connection = new Connection(
     { url: process.env.MIND_SERVER_URL || "ws://localhost:10310/mind", token: process.env.MIND_TOKEN ?? "", character: Bun.argv[2] ?? "greeen", log: console.error },
     { onPercept: (message, connection) => { if (message.sequence === 1) void connection.send({ name: "say", text: "too early" }).then((result) => console.error(JSON.stringify(result))); } },
   );
   await connection.run();
   ```
5. Ctrl+C on the echo Mind still quits Greeen cleanly, as in ticket 02.

Known gaps:
- **The echo module reads English.** The say line is worded in the listening Account's language. On a server or Account whose language isn't English, the echo Mind hears nothing.
- **NPCs are echoed too.** An NPC saying something next to Greeen as `CT_Say` in the same wording gets said back. Nothing stops two echo Minds echoing each other forever, as the spec leaves for milestone 8.
- **Fast talk gets "Slow down!"** The echo goes out as soon as the line arrives, so two lines said within half a second make the second echo fail the server's say rate limit, which Greeen is told as a system message.
- **`say` text isn't checked.** Empty text, a very long text or a line break goes to the server as a real client's command would. Malformed messages are 05's.
- **`chatLocation` lists a value it never has.** The generated `eChatLoc` includes `CL_SystemWindow`, which a message Percept never carries.
