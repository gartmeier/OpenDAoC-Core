# 01: `say` packet: capture and builder

**What to build:** A recorded, checked-in reference of exactly what a real 1.127 game client sends when a Human types one say line, and a Harness packet builder that produces those bytes. The capture sits next to milestone 1's fixtures, and their README notes what it holds and which fields vary. The builder is what the `say` Primitive will send (ticket 04). See the spec: `.scratch/mind-connection/spec.md`.

The Human starts this: with `/agent capture <account>` running, log in with the real 1.127 client, type one say line (for example `/say hello`), and stop the capture. An agent then checks in the fixture and writes the builder and its test.

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] A capture of one say line from the real 1.127 client is checked in with the other 1.127 fixtures
- [ ] The fixtures README describes the say packet: its code, layout, and any fields that vary
- [ ] The Harness has a `say` packet builder taking the text to say
- [ ] A golden-byte test shows the builder produces the captured bytes, masking only fields that legitimately vary
