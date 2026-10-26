# Porcelain the Agents keep rewriting

Status: needs-triage

## Problem Statement

Orla, Fearnan and Dalta each wrote their own Tactics, and several pieces show up in all three, written a little differently each time:

- **Releasing after death.** Each sends `/release` and waits for `!agent.self.dead`, then does something different: Orla waits a fixed 3 s because its Surroundings still held the Mob that killed it as attacking it, and `kill` ran all the way back to it. Dalta fails with `stillDead`. Fearnan just carries on.
- **Picking a Mob.** All three filter `surroundings.npcs` with the same `realm === "None" && !peaceful && healthPercent > 0` and sort by distance from their own position, each computing the distance themselves.
- **Geometry.** Orla writes its own `distance` and `headingTo`, though Surroundings already exports `distance` and `headingTowards`.
- **Examining an item.** Dalta's `armourFactor` sends `examine`, waits a fixed 800 ms and then reads `agent.inventory.examined`, instead of waiting for the `examined` Percept. A slow answer reads the last item's text.
- **Sleeping.** Every Agent writes `agent.waitFor(() => undefined, ms)` or `waitFor(() => false, ms)` to wait a while.
- **Time.** Orla and Dalta use `Date.now()` and Fearnan uses `agent.clock.now()`, so only Fearnan's Tactics could run on a test clock.

The guide lists the porcelain Tactics an Agent can use, and none of these is among them, so every new Agent writes them again.

## Solution

The Mind gains a `release` Tactic, an `examine` Tactic, `agent.sleep(ms)` and `surroundings.mobs(from)`. The guide lists them, says Surroundings' `distance` and `headingTowards` exist, and says to read the time from `agent.clock`. The three Agents' Tactics use them and drop their own copies.

## User Stories

1. As an Agent, I want to release after dying with one call, so that I don't write the wait myself.
2. As an Agent, I want `release` to succeed only once I'm alive at my Bind point, so that my Surroundings are those around the Bind point, not where I died.
3. As an Agent, I want `release` to fail with `notDead` when I'm alive, so that a stray call does nothing.
4. As an Agent, I want `release` to fail with `timeout` when I stay dead, so that my Tactic doesn't hang.
5. As an Agent, I want no Mob from where I died to look like it's attacking me after `release`, so that `kill` doesn't run me back to it.
6. As an Agent, I want the Mobs in view, nearest first, from one call, so that I don't repeat the realm, peaceful and health filter.
7. As an Agent, I want that list to leave out NPCs I can't fight, so that I never pull a guard or a merchant.
8. As an Agent, I want to filter that list by level and name myself, so that each Tactic keeps its own choices.
9. As an Agent, I want to examine an item and know the text I read is that item's, so that I don't compare the wrong armour.
10. As an Agent, I want `examine` to fail with `nothingThere` for an empty slot, so that I don't wait for an answer that won't come.
11. As an Agent, I want `examine` to fail with `timeout` when no answer comes, so that I don't read stale text.
12. As an Agent, I want `agent.sleep(ms)` to wait a while, so that a pause reads as a pause.
13. As an Agent, I want `agent.sleep` to end with `Stopped` when I'm told to stop, so that a long pause doesn't keep me going.
14. As an Agent, I want the guide to tell me Surroundings' `distance` and `headingTowards` exist, so that I don't write my own.
15. As an Agent, I want the guide to tell me to read the time from `agent.clock`, so that my Tactics behave the same on a test clock.
16. As Orla, I want `hunt` to use `release`, `surroundings.mobs`, Surroundings' geometry and `agent.sleep`, so that it loses its own copies and its 3 s wait after releasing.
17. As Fearnan, I want `grind` to use `release` and `surroundings.mobs`, so that it handles death the same as the others.
18. As Dalta, I want `grind` to use `release` and `surroundings.mobs`, and `armourFactor` to use `examine`, so that it loses its fixed waits.
19. As Orla, Fearnan and Dalta, I want my Tactics to read the time from `agent.clock`, so that they could be tested.
20. As a coding agent, I want the fighting Learning's Dying section to use `release`, so that I start from it.
21. As the Human, I want this porcelain to report only what the client knows, so that the Agent knows no more than a client (ADR 0001).

## Implementation Decisions

- **`release` Tactic.** `release(agent)` fails with `notDead` if the Agent is alive. It sends `/release` and succeeds once Self is alive and the Agent's own position has jumped to its Bind point after that. It fails with `timeout` after 30 s. Surroundings already forgets what's out of view when the Agent's own position changes, so once `release` succeeds, the Mob that killed the Agent is gone. Orla's stale Mob was seen between being revived and that jump.
- **`examine` Tactic.** `examine(agent, slot)` fails with `nothingThere` for an empty slot. It sends `examine` and succeeds once an `examined` Percept arrives after it, leaving the text in `agent.inventory.examined`. It fails with `timeout` after a few seconds.
- **`agent.sleep(ms)`.** On Agent: waits `ms` on the Agent's clock and throws `Stopped` once the Agent is stopped, as `waitFor` does.
- **`surroundings.mobs(from)`.** The Mobs in view (NPCs of realm None, not peaceful, with health above 0), nearest `from` first. Level, name and other filters stay with the caller.
- **Guide.** Its list of porcelain gains `release` and `examine`, and its list of what an Agent has gains `agent.sleep`, `surroundings.mobs`, `distance`, `headingTowards` and "read the time from `agent.clock.now()`".
- **Learning.** The fighting Learning's Dying section uses `release`, and Picking a Mob uses `surroundings.mobs`.
- **Agents' Tactics.** Orla's `hunt`, Fearnan's `grind` and Dalta's `grind`, `takeTask` and `armourFactor` use the new porcelain, drop their own geometry and their `waitFor(() => undefined, ms)` sleeps, and read time from `agent.clock`. Waits that aren't for any evidence and aren't sleeps (Dalta's 1.5 s after talking to a task guard) are left to Dalta.

## Testing Decisions

- A good test drives the Agent with Percepts on the fake clock and checks the Outcome and the Primitives sent.
- **`release`:** succeeds after `revived` and the jump. Doesn't succeed on `revived` alone. Fails with `notDead` and with `timeout`. Prior art: the `rest` and `kill` tests with the test Agent.
- **`examine`:** succeeds on the `examined` Percept after the send. Fails with `nothingThere` and `timeout`. Prior art: the `equip` tests.
- **`agent.sleep`:** resolves once the fake clock passes `ms`, and rejects with `Stopped` on `stop`. Prior art: the Agent's `waitFor` tests.
- **`surroundings.mobs`:** leaves out peaceful, realm and dead NPCs, and orders the rest by distance. Prior art: the Surroundings tests.
- **Manual acceptance test:**
  1. Start Orla, Fearnan and Dalta in turn and have each grind or hunt for 10 minutes on its rewritten Tactic.
  2. Each one that dies releases with `release` and doesn't run back to its killer.
  3. Dalta equips better armour from loot with `armourFactor` on `examine`.
  4. Record anything that went differently on this spec.

## Out of Scope

- `/train` and multi-word Specializations, which another spec handles.
- A `fightBack` Tactic. Fighting back is two lines over `surroundings.attacking`, and each Agent fights back its own way.
- An experience total on Self.
- Running back to a Grave or praying at it after `release`.
