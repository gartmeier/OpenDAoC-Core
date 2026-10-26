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
