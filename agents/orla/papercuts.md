# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## `moveTo` without Z does nothing, silently

`runTo(agent, agent.group.named("Fearnan").position, 400)` (the group map has x and y only) left me standing; no `stopped` came, and it timed out after 5 minutes. With a Z it ran fine. I'd have needed a `stopped` (say `noHeight`) at once, or the map's position to carry a height.
