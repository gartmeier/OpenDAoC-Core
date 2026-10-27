# Lessons

What I've learned while playing, which I read again before I next act.

- **Open every fight from range.** Stand about Gleam Ray's range (1500, I use ~1350) from the Mob and cast until it first hits me, then use the staff. Charging in with the staff got me killed. `tactics/hunt.ts` `pull` does this. (Advice from the Human.)
- The training staff hits for 10–14, much more than Gleam Ray's 2–6 at level 1, so it finishes the fight once the Mob is next to me.
- At level 1, level 0 sand crabs are safe. A level 1 large frog killed me twice in a row: avoid yellow Mobs until I have better gear.
- **Power comes back much faster sitting.** Rest (sit) to full Power before the next pull, so I can cast all the way in. (Advice from the Human.)
- After `/release` my Surroundings still held the Mob that killed me as "attacking me", and `kill` ran all the way back to it. Only fight back against attackers within ~500 units, and wait a few seconds after releasing.
- My bind point is in Connla, near X:295668 Y:641600, next to the Sentinels and trainers.
- Porcelain file names are kebab-case: `mind/src/tactics/run-to.ts`, not `runTo.ts`.
- Train Magician lines with `train(agent, "Light", n)` and `train(agent, "Mana", n)`, as `agent.self.specs` names them. Level 3: Light 3, which gives Gleam Blast (13 heat damage, 3 Power).
- Level 1 to 3 took 17 kills on level 0 to 2 sand crabs and beach rats south of Connla, no deaths.
- Level 3 to 4: 16 kills on level 2 to 3 Mobs, mostly mudmen, which three Gleam Blasts (15–17 each) nearly kill before they arrive. A level 3 sandman killed me at level 4: I avoid sandmen.
- Fearnan (a Naturalist) plays at the same time; `/who all` lists both of us with level and zone.
- Mudmen sometimes come at me on their own. The staff does only 3–4 damage to them and Gleam Blast 18, so meet anything after me with `pull` (Spells until it hits), never `kill` alone. Two deaths at level 4 came from fighting back with the staff only, at full Power.
- Colm (Armor Merchant, Connla, X:294740 Y:641183) buys loot, but for 1–6 copper apiece. Examine magical drops ("Fortifying", "Mighty", "Focus Staff") before selling: they may beat my level 2 woolen gear.
- Level 4 to 5: 8 kills, no deaths, once fighting back with Spells. Level 1 to 5 took about an hour.
- Promoted to Eldritch at level 5 by Nainsi (Eldritch Trainer, Connla, X:296745 Y:641480): `talkTo`, then `whisper(agent, id, "Eldritch")`. It adds Void Magic and gives the Eldritch Staff of Focus. I kept the 5 level 5 spec points unspent.
- **Quick Cast in a fight works** (`cast` with `{ quickCast: true }`, called by `pull` once the Mob reaches me): use "Quickcast", one word. The Mob's first hit often lands mid-cast without interrupting it, and Quick Cast is then refused ("can't prepare a quickcast while casting"). Wait for the cast to land or break, then Quick Cast and cast: it hit a mudman for 28 while it was hitting me. It costs double Power (8 for Major Ethereal Concussion), so keep 8 back for it.
- Level 5: all 5 spec points into Light (4 to 5), as the Human said: one line only, the one I started with. Light 5 gave Shadowblast (26 cold damage, 4 Power), now my best nuke.
- With every point in Light, Light equals my level (each level n gives n points, and Light n costs n; `wiki/classes/eldritch.md`). Train Light each level: Shadowcharge (41) at 8, Shadowshock (56) at 12, Paralyzing Veil (15 s mez) at 15. Shadow Control nukes have no recast, so chaining Shadowblast is fine; Void bolts have a 20 s recast.
- Level 5, buffed by a passing player: Shadowblast hits level 4 Mobs (feccans, villainous youths, mudmen) for 27–41, and 2–4 kill them. `hunt(agent, (l) => [l - 1, l], 7, 5, ["sandman"])` made 11 kills in 5 minutes (about 45% of level 5), with no deaths.
- A Spell's effect shows in `agent.self.effects` a second or two after `cast` succeeds, not right away.
- Level 4–6 Mobs drop a lot of Focus Staffs (4 lb each, and Colm pays 5–18 copper). At 44 of 40 lb I could still walk, slowly, but after a relog I was "heavily encumbered and cannot move". Walking did work again after a Recall Stone jump. `hunt` now stops at 35 lb: recall, sell everything at Colm and walk back. Colm won't buy the training staff.
- A level 6 Focus Staff drop gives +6 levels to all Spell lines, and Lightbender Cloth Gloves give +1 Light. Examine drops with `{ name: "examine", slot }`, then read `agent.inventory.examined`.
- Level 6 to 7 took about 50 kills of level 4 Mobs (each about 1.1–1.2% of level 6) in roughly 40 minutes, with no deaths. Shadowblast hits them for 37–41 with the Focus Staff. Light 7 at level 7.
- **Grouped with Fearnan (Druid, bear pet)**: Fearnan invites, I `answer(agent, true)` the GroupInvite Dialog. Fearnan pulls with the bear and heals; I assist with `tactics/duo.ts` (nukes whatever fights the group). When I pulled myself at level 7, a level 7 hill toad came to me before the bear took it and killed me: let the pet take aggro first.
- A Group member's line in /g aborts my running snippet (at a safe point), and mine aborts theirs. Speak once per event, only for real coordination (pulls, rest, ready, moving). Never post health or power: both Minds see each other's in `agent.group` (`healthPercent`, `powerPercent`). A rest loop that posted on every pass flooded /g ("Slow down!") and broke Fearnan's play: wait on `agent.group`, not on a loop of sends.
- Quick Cast has its own cooldown: check `agent.self.cooldowns.has("Quickcast")` before asking `cast` for it, or it fails with `cooldown` every time.
- To pray at my grave: `target` the grave's objectId (a World object "Orla's Grave"), then `/pray`.
- Duo XP at level 7: a level 6–7 hill toad gives ~260–530 (12–25‰) with camp and group bonus.
- **Always give `runTo` and `moveTo` a real Z: the target's own `position.z`** (a Mob's, a player's in view, a known spot's), never 0 or a made-up value. (Rule from the Human.) Without Z, `runTo` to Fearnan's map position stood still for 5 minutes and timed out. The group map has no Z: `duo.ts` `partnerAt` takes the Z Fearnan last had in view.
- **Kill Tasks in a group:** Sentinel Grania (X:314041 Y:636746 Z:4849) gives level 7 a lugradan whelp (L4–6, nearest ~X:310500 Y:634900). With Fearnan (L8) and me both on the whelp task, one shared kill counted for both of us, and each turned in for 1329 XP (61‰ at level 7) + ~2.5s. But the L4 whelp gave no kill XP (grey to Fearnan). A task loop is ~3–4 min, ~15–25‰/min; pulling rowdies as a group gave ~29‰/min, so we pull and skip tasks for now.
- Rowdies L6–8 near X:321200 Y:643300 (Fearnan's camp): ~1 kill a minute as a duo, 450–540 XP each with camp bonus. Wait until the pet has the Mob at 75% before nuking, or it turns on me and every cast is interrupted.
- A relog of either of us drops me from the Group; Fearnan re-invites and `duo` now accepts the invite itself.
- Slevin (Shield Merchant, X:309594 Y:645580 Z:4849, by Torrance) is the merchant nearest the rowdies.
