# A Primitive's result reports dispatch only

A Primitive's result says only whether the Harness queued its packet for the client tick. It never says whether the packet handler accepted it, rejected it or ignored it. The Harness runs inside the server and could tell, but a real client is never told, so reporting it would break ADR 0001's rule that an Agent knows only what a client knows. Whether a Primitive had its effect is judged from Percepts, as a real client must. This follows rs-sdk, whose low-level calls confirm only that the client dispatched a command.

## Considered Options

- **Report the handler's outcome**: simpler Tactics and fewer silent failures, but it tells the Agent about rejections that no client sees.

## Consequences

- A Primitive the server silently ignores looks like success. Code that waits for evidence of the effect (milestone 4) has to catch this from Percepts and timeouts.
- Don't "improve" Primitive results with server-side knowledge. Any failure signal must come from something the client is told.
