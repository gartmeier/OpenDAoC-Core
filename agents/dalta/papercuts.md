# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## Debuffs on a Mob show only in chat (2026-10-24)

When Return Blade slowed a rock, only the chat said so ("can't attack as fast as normal", then "attacks return to normal"). A Human sees this only in chat too: the server sends no packet for the effects on a target. My own buffs and debuffs are in `agent.self.effects`.

## `train` can't train a line with a space in its name (2026-10-24)

`train(agent, "Celtic Dual", 3)` sends `/train Celtic Dual 3`, which the server ignores without a word, so it fails with `notTrained`. Sending `/train "Celtic Dual" 3` myself worked.
Needed: the porcelain quotes the line name.

## `kill` times out on the run to a far Mob (2026-10-25)

`kill` starts its 30 s `noDamage` clock before running to the Mob, so a Mob more than ~5500 units away failed with `noDamage` while I was still running, every time (`grind` then tried again from closer and won).
Workaround: `grind` runs within 800 units of a Mob more than 2000 away before calling `fight`.
Needed: the clock starts once in melee range, or `kill` fails with a separate reason while the run is going.

## A `/train` right after another is sometimes ignored (2026-10-25)

At level 18, `trainBlades` sent `/train Blades 18`, then `/train "Parry" 12` and `/train "Celtic Dual" 12` one after the other, each waiting for its spec to change. Blades and Celtic Dual trained, Parry didn't, with no message at all; the same command a minute later worked.
Workaround: `trainBlades` now waits a second before each line and tries twice.
Needed: an answer from the server (or a Percept) when a `/train` is refused, so the Mind knows why.

## Any `execute_code` call after `/quit` logs the Character back in

I sent `/quit`, sat out the 20 s countdown, then ran a 3 s snippet to check I was gone: the Mind said "Dalta wasn't in the world, and has entered it first" and I was back in Basar. A Human who quits stays out. To log out for good, the `/quit` snippet has to be my last call. I'd have needed a way to see whether I'm in the world without entering it.
