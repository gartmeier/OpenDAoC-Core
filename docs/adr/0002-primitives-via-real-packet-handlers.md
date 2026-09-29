# Primitives go through the real packet handlers

Primitives are turned into synthesized inbound packets for a single fixed client version (1.127). Those packets are processed by the same packet handlers and at the same point in the client tick as a Human's packets, rather than by calling `GamePlayer` methods directly. Some validation (range, cooldowns, state checks) lives only in the handlers, and the experiment depends on Agents playing under exactly the rules Humans do. Calling `GamePlayer` directly looks simpler and is a deliberate non-choice: don't "simplify" to it.

## Consequences

- The Harness depends on 1.127 inbound packet layouts; packet builders need tests that round-trip through the handlers' read logic.
- Because the game client owns its own position, the Harness must simulate movement and emit position updates itself.
