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

**Status:** ready-for-agent

- [ ] `/say hello` from the Human next to the Agent makes the Agent say `hello` back, once
- [ ] The Agent's own say line doesn't make it say anything
- [ ] Chat that isn't say, and system messages, don't make it say anything
- [ ] Unit tests: the message call gives a Percept with exactly its text, chat type and location; a call without a projection still gives a bare Percept
- [ ] A `say` sent while the Agent isn't in the world is refused with a reason
- [ ] `bun test` covers the echo module: another speaker's say line, the Agent's own line, other chat types, unparsable text
- [ ] The regenerated TypeScript protocol types include the message Percept and `say`, and the codegen test passes
