# 01: Movement: capture and builders

**What to build:** A recorded, checked-in reference of exactly what a real 1.127 game client sends while its Character runs, turns and stops, and the Harness packet builders that produce those bytes. The capture sits next to the existing 1.127 fixtures, and their README gets a Movement section. The builders are what the running position updates (ticket 03) and the `face` Primitive (ticket 04) will send. See the spec: `.scratch/movement/spec.md`.

The Human starts this. With `/agent capture <account>` running, log in with the real 1.127 client and, on flat open ground:
1. stand still for a few seconds;
2. run in a straight line for a few seconds, then stop;
3. turn on the spot without moving (keyboard turn), then stop turning;
4. run past an NPC;
5. stop the capture with `/agent capture off`.

An agent then checks in the fixture and writes the builders and their tests.

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] A capture of running, turning and stopping from the real 1.127 client is checked in with the other 1.127 fixtures, unmodified
- [ ] The fixtures README has a Movement section: how often a running client sends position updates, which position update fields change while running (speed, Z speed, flags, heading flags), what a heading update (0xBA) holds, and what a stop looks like
- [ ] The Harness has a builder for a running position update, taking at least position, zone, heading and speed
- [ ] The Harness has a builder for a heading update
- [ ] Golden-byte tests show both builders produce the captured bytes, masking only fields that legitimately vary
- [ ] The README's table of fields that vary covers anything new the capture shows
