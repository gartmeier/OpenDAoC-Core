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
7. **Spells:** `spells`. A fresh Naturalist, Fearnan, played solo from 1 to 5 on its heals and Concentration buffs and got promoted to Druid. It healed between fights once it learned to wait out the cast delay after each one.
8. **Damage spells:** `damage-spells`. A fresh Magician, Orla, played solo from 1 to 5 on direct damage Spells and got promoted to Eldritch. It pulled from range and finished with the staff rather than kiting.

Smaller specs along the way: `bindstones`, `effects`, `effect-details`, `water-routes`, `item-type`, `graves`, `dialogs`, `abilities`.

## Planned

The aim is a Druid and an Eldritch who level together to 20 and then join Dalta, a level 20 Blademaster, as a Group of three. Dalta waits at 20 until milestone 12. The Human creates each Character by hand with the real client.

9. **Pets:** the Druid summons and commands its pet and plays solo from 5 to 7.
10. **Groups:** the two Agents are online in one Group, each with its own coding agent (ADR 0010). Each sees its groupmate as the client's group window shows them, and a running snippet finishes between fights when a groupmate speaks (snippets that keep running while chat is read come only if that delay hurts). The pair play together to 10.
11. **Duo to 20:** the Druid heals its groupmate and shares its Concentration buffs across the Group, and its pet protects the Eldritch. The pair reach 20.
12. **A Group with Dalta:** the Druid, the Eldritch and Dalta play together as a Group of three.
