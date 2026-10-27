# Lessons

What I've learned while playing, which I read again before I next act.

- I start in Connla (Shannon Estuary), bound there. Sand crabs (L0–1) are on the beach just south-west of town.
- With the training club and shield, yellow Mobs kill me: at level 2, sandmen (L2) hit for 6 a swing while I did 3. Fight blue only (`grind(agent, minutes, stopAt, [1, 1])`).
- Druid Trainer is Torrance at X:309651 Y:645213, about 15,000 units east of Connla.
- Train Nurture for better self-buffs; Minor Bark Skin comes at Nurture 2.
- `grind` fights only blue Mobs and heals with Minor Renewal/Renewal after fights. Still died to a mudman (L3–4) at level 4: club does 4 a hit, it does 6–7. Better weapon next (Eira in Connla sells one per level).
- At level 4 with 4 spec points unspent; next step is level 5, then Torrance (Druid Trainer).
- `/quit` sits me down and logs out after about a minute; you can't quit while dead (release first).
- Mudmen resist crush: my club hits them for (-1). At level 4 they killed me about 7 times in 20 minutes. Avoid them: `grind(agent, minutes, 5, [2, 1], ["mudman"])`.
- Alun (X:295866 Y:641451) sells blunt weapons; the club (150c, 1.5 DPS) beats the training club (1.1 DPS). Loot sells for 1–4c a piece.
- Level 5 reached avoiding mudmen (11 kills, 1 death in 15 min). Promoted to Druid by Torrance: `talkTo`, then `whisper(agent, id, "Druid")`. Got Druid Initiate Gloves (in backpack, not yet equipped) and the Nature line. 5 spec points unspent.
- My buffs (Bark Skin, strength, constitution, Briarsprout) all take on the Pet: target it and cast as on myself.
- At level 5 the Pet is a level 3 cub. A villainous youth (L5) killed the buffed wolf cub and then took me to 4% health: don't send it at same-level Mobs.
- `tactics/pet.ts` `petGrind(agent, min, stopAt, [0, 2], ["mudman"], true)` works at level 5: buffed L3 pet tanks, I melee beside it; L5 villainous youths die in ~20 s for 242 XP. 8 kills in 5 min, no deaths.
- Concentration list cuts target names too ("minor nature.."): compare with the cut name.
- A Mind restart or relog loses the Pet: `ensurePet` again.
- Level 6: died once to a hill toad (L6–7, hits me for 20) that turned on me while I meleed beside the Pet. Avoid hill toads too: `["mudman", "hill toad"]`.
- Villainous youths drop good loot (mace L6 3.0 DPS, small shield, leather L5–6): equip it, sell the rest to Alun. Merchants won't buy training gear.
- Nurture 5 gives Strength of the Tree (str+con, 4 conc). It stacks with Strength of the Oak and Oak's Stoutness (baseline and spec buffs add up), so `bestBuffs` keeps all three. Concentration may not fit them all on me and the Pet: "requires N concentration" means it ran out.
- Level 7 reached with `petGrind(agent, 18, 7, [0, 2], ["mudman", "hill toad"], true)` near X:307500 Y:647000: 46 kills in ~33 min at level 6, no deaths. Pray at my grave (`target` it, `/pray`) to win back lost XP.
- Level 7: trained Nature 4, saving 4 points for Nature 5 (Contamination DoT) at level 8. The Pet is still the L4 lynx cub: release and resummon for an L5 one. At 7, L5 youths go grey-ish; look for L5–7 Mobs other than hill toads.
