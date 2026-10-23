# A stock coding agent plays through the Mind

Following rs-sdk, the LLM side of an Agent is an ordinary coding agent, such as Claude Code. It plays by running TypeScript snippets against the Mind's Primitives, Self and Surroundings through a tool. It writes its own Tactics as files, and keeps its notes and Lessons as files. A guide in the repo teaches it the workflow and the game. The coding agent already plans, writes code, watches the outcome and revises, so the Mind adds no Planning or Reflection loop of its own. The work goes into giving it enough context and enough Primitives, and growing both from where it fails, as rs-sdk grew its harness.

This replaces the plan of milestones 5 (LLM Planning) and 6 (Reflection and Tactic Library), which would have built that loop around the Mind. Plan, Planning, Reflection and Tactic Library are no longer glossary terms.

## Considered Options

- **Our own Planning and Reflection loop**: the Mind calls an LLM to keep a Plan of goals, and another pass reviews outcomes to write Tactics and Lessons. That's more control over prompts and memory, but it's a large system to build before the LLM plays at all, and it would redo what coding agents already do well.

## Consequences

- An LLM-written Tactic is just a TypeScript file in the shape of ADR 0008, and nothing sandboxes it (ADR 0003).
- Game knowledge reaches the LLM as documents in the repo, as rs-sdk's guide, learnings and wiki do.
- A Primitive or Percept is added when the coding agent turns out to need it, not ahead of time.
