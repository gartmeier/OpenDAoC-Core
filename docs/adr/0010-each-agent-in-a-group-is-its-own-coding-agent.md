# Each Agent in a Group is its own coding agent

When Agents play together, each keeps its own coding agent, Mind and Character (ADR 0009), and they talk to each other only through the game's own chat, as two Humans in a Group would. Teamwork inside a fight, such as healing a groupmate whose health falls, is Tactic code each Agent runs, because an LLM turn takes seconds and a fight needs answers in under one. Only the slower coordination between fights, such as where to hunt, when to rest and who needs which buff, goes through the LLMs and so through chat. Each Agent decides for itself, the Human can read and join the conversation in game, and an Agent still owns exactly one Character.

## Considered Options

- **One coding agent driving every Character in the Group**: no messaging problem and one coherent plan, but it's one Agent with several Characters, its Lessons mix classes, and there's no collaboration left to study.
- **A lead session orchestrating one sub-agent per Character**: the sub-agents would run in parallel and pass messages through the lead, which is the chosen option with a layer the game doesn't need.

## Consequences

- An Agent hears its groupmates only when chat reaches its coding agent, which is when a snippet ends. So when a groupmate speaks, the Mind asks the running snippet to finish at its next safe point, between fights, and porcelain loops honour that. A coding agent hears its groupmate about one fight late.
- Letting `execute_code` return on a groupmate's chat while the snippet keeps running, with a `wait` tool to carry on waiting, is a later option if that delay turns out to hurt.
- What an Agent knows about its groupmates, such as their health, comes from Percepts of what the client's group window shows, never from the other Agent's Mind.
