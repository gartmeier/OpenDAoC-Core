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

**Status:** resolved

- [x] A capture of running, turning and stopping from the real 1.127 client is checked in with the other 1.127 fixtures, unmodified
- [x] The fixtures README has a Movement section: how often a running client sends position updates, which position update fields change while running (speed, Z speed, flags, heading flags), what a heading update (0xBA) holds, and what a stop looks like
- [x] The Harness has a builder for a running position update, taking at least position, zone, heading and speed
- [x] The Harness has a builder for a heading update
- [x] Golden-byte tests show both builders produce the captured bytes, masking only fields that legitimately vary
- [x] The README's table of fields that vary covers anything new the capture shows

## Comments

**2026-10-20 (agent):** Done. The Human recorded the capture; it's checked in as `Tests/Fixtures/Agents/v1127/run-turn-stop.tsv` (all of `admin-20261020-123707-695.tsv`, unmodified apart from a description header), with a Movement section in the fixtures README (03b4e7724). The builders and their golden-byte tests are in 840aebe50.

What the capture shows (details and full packets in the README):
- **Running:** a position update every 200 or 233 ms (173–260 ms, mean 212 ms), in place of the idle 2130 ms. Only X, Y, speed and, when steering, the heading change. Z speed stays 0 (flat ground), state flags 0, action flags `TORCH`. The first update goes out as soon as the Character moves, not on the idle cadence, already with a small speed; the speed ramps 19.6, 72.6, 127.2, 180.9, then 191 (about 0.9 s). Heading `h` points along (−sin θ, cos θ), θ = `h` × 2π / 4096, matching the run's direction.
- **Stopping:** one update with the speed falling (about 100) and then one with speed 0 about 200 ms later, at the final spot. Then the idle cadence resumes.
- **Turning on the spot:** heading updates (0xBA) every 200 or 233 ms with the heading reached so far; position updates stay on the idle cadence. 0xBA data: session ID, 2 zero bytes (the server reads a target), the 12-bit heading, 0, action flags, 0, 0, presumably health % (100), 3 zero bytes.
- **Heading flag 0x1000:** set in every position update's heading here, and in all but one in the login captures; never in heading updates. The Harness's idle position update sends `player.Heading` without it today, which has worked; ticket 03 may want to send it while running to match the capture.
- **Target:** data offsets 22–23 of the position update are the target's object ID (the old comment said "object ID"). The Human targeted the NPC after the second run.
- **Strafing:** the Human strafed right before turning. It shows as state flags 0xA0 with speed 0, so a strafing update can't be told from standing by its speed.

Builders in `ClientPackets1127`:
- `PositionUpdate` now takes a `speed` after the heading (0 standing, as before; `HarnessClient.PositionUpdate` passes 0). Z speed and target stay 0.
- `HeadingUpdate(heading, healthPercent, sessionId, actionFlags)`.

Tests: `RunningPositionUpdate_ShouldMatchCapture` and `HeadingUpdate_ShouldMatchCapture` check one packet each with literal values; `EveryRunningPositionUpdate_ShouldMatchCapture` and `EveryHeadingUpdate_ShouldMatchCapture` check every running and heading update in the captures, taking only position, speed and heading from the captured packet. `Running_ShouldSendPositionUpdatesAboutFiveTimesASecond` pins the running rate for ticket 03. The checksum fixture test covers the new capture. Nothing is masked beyond the common header fields. All 171 unit tests pass (`-c Release`).

Not captured: running uphill or downhill (Z speed while running), and a heading update while something is targeted.
