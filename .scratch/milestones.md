# Milestones

Each milestone ends with an Agent playing. Its spec is `.scratch/<slug>/spec.md`. Smaller specs come up between milestones whenever an Agent turns out to need something (ADR 0009).

## Done

1. **Login and logout:** `agent-harness-login`
2. **Mind connection:** `mind-connection`
3. **Movement:** `movement`
   - 3b. **Terrain and pathing:** `terrain-pathing`
4. **Hand-written grind:** `grind`
   - 4b. **Inventory and gear:** `inventory-gear`
5. **A coding agent plays:** `coding-agent`
6. **Styles:** `styles`

Smaller specs along the way: `bindstones`, `effects`, `effect-details`, `water-routes`, `item-type`, `graves`, `dialogs`.

## Planned

The aim is a Druid and an Eldritch who level together to 20 and then join Dalta, a level 20 Blademaster, as a Group of three. Dalta waits at 20 until milestone 12. The Human creates each Character by hand with the real client.

7. **Spells:** `spells`. A fresh Naturalist plays solo from 1 to 5 with its heals and Concentration buffs, until it's promoted to Druid.
8. **Damage spells:** a fresh Magician plays solo from 1 to 5 on bolts, kiting, until it's promoted to Eldritch.
9. **Pets:** the Druid summons and commands its pet and plays solo from 5 to 7.
10. **Groups:** the two Agents are online in one Group, each with its own coding agent (ADR 0010). Each sees its groupmate as the client's group window shows them, and a running snippet finishes between fights when a groupmate speaks (snippets that keep running while chat is read come only if that delay hurts). The pair play together to 10.
11. **Duo to 20:** the Druid heals its groupmate and shares its Concentration buffs across the Group, and its pet protects the Eldritch. The pair reach 20.
12. **A Group with Dalta:** the Druid, the Eldritch and Dalta play together as a Group of three.
