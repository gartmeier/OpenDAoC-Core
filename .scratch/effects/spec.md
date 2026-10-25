# Effects on the Self

Status: resolved

## Problem Statement

Dalta, a coding agent, takes a Hastener's speed buff before a long run. It lasts 10 minutes and a fight ends it, but Dalta can't see it: a Human sees it in the buff bar with its time left, while the Mind gets the server's buff bar only as a bare Percept with no contents. All Dalta has is `agent.self.maxSpeed` going from 191 to 281, which says something sped it up but not what, nor for how long. Dalta can't plan a run around the buff's time left, can't tell whether a fight ended it, and can't see any other buff or debuff on its own Character at all, such as a Mob's debuff on it.

## Solution

The Harness turns the server's buff bar into an Effects Percept carrying every Effect on the Agent's Character, each with its name, whether it's a buff or a debuff, and its time left. The Self keeps them as `agent.self.effects`, counting each Effect's time left down as a client's buff bar does, until the server says it's gone. A Mind that takes over is told the bar anew. The Agent then knows what a Human sees in their buff bar, and no more.

## User Stories

1. As an Agent, I want to see every Effect on my Character, so that I know what's helping or hurting me.
2. As an Agent, I want each Effect's name, so that I can tell a Hastener's speed buff from any other.
3. As an Agent, I want to know whether an Effect is a buff or a debuff, so that I can react to debuffs without knowing every Spell's name.
4. As an Agent, I want each Effect's time left in seconds, so that I can plan a run around how long my speed buff lasts.
5. As an Agent, I want an Effect's time left to count down between updates from the server, so that it's right whenever I read it, as a client's buff bar is.
6. As an Agent, I want an Effect without a timer to have no time left rather than zero, so that I don't believe a lasting Effect is about to end.
7. As an Agent, I want an Effect whose time has run out to stay until the server removes it, so that I never believe a buff is gone before it is.
8. As an Agent, I want an Effect's time left to stop at zero, so that I never read a negative time.
9. As an Agent, I want an Effect to disappear when the server ends it, such as when a fight ends my speed buff, so that I know it's over.
10. As an Agent, I want an Effect to appear as soon as a Spell lands on me, so that I can wait for a Hastener's buff rather than guess from my speed.
11. As an Agent, I want a refreshed Effect to show its new time left, so that recasting a buff resets my plan.
12. As an Agent, I want to see debuffs a Mob puts on me, so that I can tell why I'm slowed or weakened.
13. As an Agent, I want to know nothing about my Effects before the server tells me, so that what I believe comes only from Percepts.
14. As an Agent, I want my Effects at world entry, so that I know what's still on me after logging back in.
15. As an Agent whose Mind takes over, I want the new Mind to be told my Effects anew, so that it doesn't start blind to buffs that are still on.
16. As an Agent, I want to see only my own Effects and not my target's, so that I know no more than a Human with the real client.
17. As an Agent, I want effects the client doesn't show in the buff bar left out, so that my Effects match what a Human sees.
18. As an Agent writing a Tactic, I want to wait until an Effect is on me, so that a Tactic taking a Hastener's buff can check it worked by name.
19. As an Agent writing a Tactic, I want to check an Effect's time left before a long run, so that I take the buff again if it's about to end.
20. As an Agent, I want the Learnings and my Lessons to say how to tell my speed buff is on, so that I check my Effects rather than my speed.
21. As a Human studying the Journal, I want the Effects Percept recorded with its contents, so that I can see what the Agent knew about its buffs and when.
22. As a Human, I want Effect in the glossary, so that the Agents, the docs and I use one word for buffs and debuffs alike.

## Implementation Decisions

- **Glossary.** CONTEXT.md gains **Effect**: a buff or debuff on a Character, shown in its buff bar with its time left. _Avoid_: buff, when meaning both. The **Self** entry gains "which Effects are on it".
- **Effects Percept.** A new Percept, kind `effects`, carries the whole buff bar: a list of Effects, each with a name, whether it's positive (a buff) or not (a debuff), and its time left in whole seconds, absent for an Effect without a timer.
- **Projection.** The projection of the server's buff bar call mirrors the 1.127 packet library's: it reads the player's effect list when the call arrives and keeps the effects the packet would list, in its order, leaving out those without an icon and pulse effects. Both overloads of the call give the same Percept, and their arguments are ignored.
- **Whole bar, not a delta.** The real packet sends only the bar slots that changed. The Percept carries the whole bar every time instead, so the Mind's copy can't drift from the server's. It tells the Mind nothing a client doesn't end up showing. The server's per-effect bookkeeping for that delta is left untouched.
- **No new Harness state.** The projection needs nothing remembered between calls.
- **Takeover.** The calls the Harness makes to tell a new Mind what world entry tells a client gain the buff bar, after the money and before the points, in the order world entry sends them.
- **Self.** The Self gains `effects`: the list from the last Effects Percept, replaced whole by each one, empty before the first.
- **Time left.** Each Effect on the Self keeps when it ends on the Mind's clock, worked out when its Percept arrives, and gives `secondsLeft` from it: whole seconds, never below zero, absent for an Effect without a timer. An Effect whose time is up stays until a later Percept leaves it out.
- **Self takes the clock.** The Self is given the Agent's Clock when the Agent builds it, so time left counts down on the same clock Tactics use.
- **Protocol.** The Effects Percept is added to the Mind protocol and its generated TypeScript types like every other Percept.
- **Docs.** The "Buffs and debuffs are invisible" papercut shrinks to debuffs on a Mob, noted as something a Human sees only in chat too. The Hastener section of the moving Learning and Dalta's Lesson about the Hastener check `agent.self.effects` for the buff instead of `agent.self.maxSpeed`. No ADR: nothing here is hard to reverse.

## Testing Decisions

- Tests check what goes in and comes out at a seam: the Percept a server call gives, the calls a Takeover makes, what the Self believes after Percepts. They don't check how the projection walks the effect list or how the Self stores endings.
- **Projection** (the existing projection tests): given a test player with Effects, the buff bar call gives an Effects Percept with each Effect's name, positive flag and seconds left, in order; effects without an icon and pulse effects are left out; an Effect without a timer has no time left; both overloads give the same Percept; a player without Effects gives an empty list. The world test doubles gain a way to put Effects on a test player.
- **Takeover** (the existing Harness client tests): the test of what a Takeover tells the new Mind expects the buff bar call in its place.
- **Self** (the existing Self tests): the Self has no Effects before the first Percept; an Effects Percept gives its Effects; a later one replaces them whole, so an Effect it leaves out is gone; `secondsLeft` counts down on a fake clock, stops at zero and the Effect stays; an Effect without a timer has no time left.
- Prior art: the max speed projection and the Self's max speed tests for a Percept the Self keeps; the Takeover test for the calls world entry makes; the Agent's fake clock in the Tactic tests for time.

## Out of Scope

- Effects on a target or any other Character. The server sends no packet for them, so a Human sees them only in chat and spell animations, and so does the Agent.
- The concentration list of buffs the Agent keeps up on others.
- Group members' Effects in the group window, until Agents group.
- An Effect's icon, its tooltip and delve, and whether it's marked immune or disabled.
- Cancelling an Effect.

## Further Notes

- The speed buff's name is whatever the server's Spell calls it. A Learning should name it once someone has seen it in `agent.self.effects`.
- World entry already sends the buff bar, so a newly entered Agent gets its Effects without any change to world entry.
