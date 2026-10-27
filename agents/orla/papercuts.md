# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## `moveTo` without Z does nothing, silently

`runTo(agent, agent.group.named("Fearnan").position, 400)` (the group map has x and y only) left me standing; no `stopped` came, and it timed out after 5 minutes. With a Z it ran fine. I'd have needed a `stopped` (say `noHeight`) at once, or the map's position to carry a height.

## A robe bought in Connla can't be equipped: "from another realm"

Sorcha (Armor Merchant, Connla) sells a "linen plain robe" (L5 Cloth, 4000c; `wiki/merchants/sorcha-shannon-estuary.md`). I bought it and `equip` to the torso failed with "You cannot equip an item from another realm!". Selling it back returned half. I'd have needed the merchant's window (or the Wiki) to say which realm an item is for, or a Hibernian merchant not to sell it.
