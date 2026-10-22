# Tactics are async functions over Primitives

Following rs-sdk, the Mind has two layers. Primitives are the plumbing: one per client interaction, reporting dispatch only (ADR 0004). Tactics are everything above them: each is an async function that issues Primitives, runs other Tactics, and waits on Self and Surroundings with timeouts for evidence that what it did had its effect. It ends having succeeded, or having failed with a reason. Human-written Tactics, such as killing a Mob or resting, are the porcelain that later LLM-written Tactics build on, the way rs-sdk's `bot.*` methods sit on its `sdk.send*` calls. Straight-line async code is how both a Human and an LLM write "do this, then wait for that" most naturally.

## Considered Options

- **State machines fed one Percept at a time** (`decide(percept) → Primitive`, as the Come Mind was first written): no async machinery, but every wait becomes an explicit state, and a Tactic that runs other Tactics means nesting machines by hand.

## Consequences

- Come is rewritten as a Tactic. There are no state-machine Tactics.
- Since a Primitive's result never says whether it worked, every Tactic that waits needs a timeout, so a Primitive the server silently ignores ends the Tactic as failed instead of hanging it.
- Stopping a Tactic (a `stop` said to the Agent, Ctrl+C) must also stop the Tactics it is running.
