# 01: `say` packet: capture and builder

**What to build:** A recorded, checked-in reference of exactly what a real 1.127 game client sends when a Human types one say line, and a Harness packet builder that produces those bytes. The capture sits next to milestone 1's fixtures, and their README notes what it holds and which fields vary. The builder is what the `say` Primitive will send (ticket 04). See the spec: `.scratch/mind-connection/spec.md`.

The Human starts this: with `/agent capture <account>` running, log in with the real 1.127 client, type one say line (for example `/say hello`), and stop the capture. An agent then checks in the fixture and writes the builder and its test.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] A capture of one say line from the real 1.127 client is checked in with the other 1.127 fixtures
- [x] The fixtures README describes the say packet: its code, layout, and any fields that vary
- [x] The Harness has a `say` packet builder taking the text to say
- [x] A golden-byte test shows the builder produces the captured bytes, masking only fields that legitimately vary

## Comments

**2026-10-20 (Human):** Captured as account `admin` (Character Vue) rather than `agent1`: a say line's bytes don't depend on the Account. Typed plain `hello` with no slash, then `/send greeen hello`.

**2026-10-20 (agent):** Done in two commits (f2b3cff75, d6a75c5ae):
- The capture is checked in unmodified as `Tests/Fixtures/Agents/v1127/say-hello.tsv`, and the README's new Say section documents the packet.
- A chat line typed without a slash goes out as the `/say` command's `&s` alias: command 0xAF, parameter 0xFFFF, data `00` `&s hello` `00`. That's the layout milestone 1 found for `&q`, so `ClientPackets1127.Say(text)` is `Command("&s " + text)`. The server's `say.cs` registers `&s` as an alias of `&say`.
- Only the common header fields vary: sequence, session ID, wire byte 8 (0x38 here, leftover client memory) and the checksum. The golden-byte test `Say_ShouldMatchCapture` masks no data offsets, and the checksum fixture test now covers the new capture. All 115 unit tests pass.

Not captured: `/say hello` or `/s hello` typed with a slash. They presumably send `&say hello` or `&s hello` in the same layout. The builder mirrors the plain chat line, the way a Human usually talks.
