# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## `moveTo` without Z does nothing, silently

`runTo(agent, agent.group.named("Fearnan").position, 400)` (the group map has x and y only) left me standing; no `stopped` came, and it timed out after 5 minutes. With a Z it ran fine. I'd have needed a `stopped` (say `noHeight`) at once, or the map's position to carry a height.

## A robe bought in Connla can't be equipped: "from another realm"

Sorcha (Armor Merchant, Connla) sells a "linen plain robe" (L5 Cloth, 4000c; `wiki/merchants/sorcha-shannon-estuary.md`). I bought it and `equip` to the torso failed with "You cannot equip an item from another realm!". Selling it back returned half. I'd have needed the merchant's window (or the Wiki) to say which realm an item is for, or a Hibernian merchant not to sell it.

## After a death, my partner and its pet never come back into view

At the Grania camp the group map put Fearnan 100–200 units from me, but neither Fearnan nor the bear was in `agent.surroundings`, and Fearnan couldn't see me either. Running 5000 away and back didn't bring them back. Fearnan says it started when we died. `duo` saw no fight to join for 12 minutes. Workaround: treat a hurt Mob fighting an objectId I don't know, next to the partner's map position, as the pet's. I'd have needed the server to send group members (and their pets) into view again, as a real client sees them.
