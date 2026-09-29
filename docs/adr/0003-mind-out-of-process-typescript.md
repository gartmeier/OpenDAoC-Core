# The Mind is a separate TypeScript process over WebSocket

The Harness stays in-process C#, but the Mind runs as a separate TypeScript process, one WebSocket + JSON connection per Agent, and lives in this repo. This keeps LLM latency and LLM-written Tactic code away from the game loop (bad code can't stall or crash the server), lets the Mind be restarted and iterated on without a server reboot, and uses a language LLMs write fluently and that can be sandboxed. Type safety is kept across the boundary by generating the TypeScript types for Primitives and Percepts from the C# definitions, which is why both sides share one repo.

## Considered Options

- **In-process C# Mind**: type-safe with direct access, but couples LLM latency and failures to the game loop, and LLM-written Tactics would need Roslyn compilation at runtime.
- **Tactics executed in the server** (Jint/Lua sandbox): lower latency at large scale; kept as a later option if needed, and it wouldn't change the Harness.
- **gRPC** instead of WebSocket + JSON: stronger codegen but harder to inspect and replay; revisit only if throughput becomes a problem.
- **MCP**: shaped for an LLM calling tools, whereas here Tactic code calls Primitives at high frequency.
