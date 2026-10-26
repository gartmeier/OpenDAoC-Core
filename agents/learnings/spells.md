# Spells

A Spell is magic you cast from one of your Spell lines. A baseline Spell line gives you its Spells as you level, and any other as you train its Specialization. Your class's page in `wiki/classes/` lists every Spell line of each Specialization, with the level each Spell comes at and what its tooltip shows. Learned from how the server handles Spells, and by Fearnan, a Naturalist who healed and buffed itself from 1 to 5, and Orla, a Magician who fought with its Spells from 1 to 5.

## Which Spells you have

`agent.self.skills` lists them with `page === "Spells"`. Each has `details`, what its tooltip shows when a Human hovers its icon (a Style's `details` are a different shape, so check `page` first): what it does, its `powerCost` or `concentrationCost`, `castingSeconds` and `range`. A new one appears as you level, and a better heal of the same kind takes the place of the one before.

```ts
return agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .map(({ name, details: d }) => `${name}: ${d?.description} ${d?.powerCost ? `${d.powerCost} power` : `${d?.concentrationCost ?? 0} concentration`}, ${d?.castingSeconds} s, range ${d?.range}`);
```

## Casting one

`cast(agent, spell, targetId)` casts a Spell at a target and ends once it has landed, with an Outcome. A heal or a buff on yourself takes your own `agent.self.objectId`. It targets `targetId` itself, stands you up (you can't cast sitting) and stops a run first (moving interrupts the cast). Your attack stops by itself once a Spell with a casting time starts.

```ts
const me = agent.self.objectId;
if (me === undefined) return "not in the world yet";

return await cast(agent, "Minor Revigoration", me);
```

It fails before it sends anything with:

- `noSpell`: you haven't the Spell. Style names don't work.
- `cooldown`: it's on Cooldown.
- `noPower`: it costs more Power than you have.
- `wrongTarget`: a heal or a buff aimed at a Mob, which the server would drop without a word.

Otherwise with what the server answered:

- `noEffect`: it landed but didn't take. The target resisted, it had nothing to heal, or a better buff of its kind is up.
- `interrupted`: something hit you before its halfway point.
- `mustWait`: "You must wait N seconds to cast a spell!" three times. `cast` waits out each one and casts again by itself, so this means something is still swinging at you.
- `tooFar`, `notInView` (behind you or out of sight), `noTarget` or `noPower`.
- `refused`: any other refusal, with the server's words in the log.
- `timeout`: no answer at all.

`agent.self.casting` is your cast in progress, with the Spell and when it should land. `agent.self.cooldowns` has each Spell and Ability on Cooldown, with when it's ready again.

How an attack interrupts, from the server's code:

- Every swing at you starts an interrupt timer as long as the swing, a few seconds: a miss, a block, a parry or an evade as much as a hit. Only a roll spares you, and only against a Mob below you: 5% for each level it's below you.
- An attack doesn't stop your cast on the spot. While the timer runs, a cast you've started is interrupted when it reaches its halfway point, and you can't start a new one: "You must wait 2 seconds to cast a spell!".
- An attack after your cast's halfway point doesn't interrupt it. So a cast often lands with the Mob already on you.
- Your own swing holds your next cast off the same way, for as long as your swing. A fight ends with your killing blow, so the first cast after every kill gets "You must wait".

What to do when it fails:

- `mustWait` and `interrupted` clear up once nothing is swinging at you: kill the Mob or get away from it first.
- `noPower`, `noTarget`, `tooFar` and `notInView`: nothing changes until you do something about it.

## Healing between fights

Heal between fights, not in them: a Mob hitting you keeps interrupting your casts and stops you starting new ones, every swing of yours holds the next cast off for a few seconds, and casting turns your attack off. Heals cost Power, which `agent.self.power` and `powerPercent` show. `rest` sits until both your health and your Power are back.

```ts
const me = agent.self.objectId;
if (me === undefined) return "not in the world yet";

const heal = agent.self.skills.filter((skill) => skill.page === "Spells").find((spell) => spell.details?.description.startsWith("Heals"));
if (!heal) return "no heal";

while (agent.self.healthPercent < 90 && agent.self.power >= (heal.details?.powerCost ?? 0)) {
  const healed = await cast(agent, heal.name, me);
  if (!healed.ok) return healed;
}

return await rest(agent, 100);
```

`kill` targets the Mob again itself, so nothing needs undoing after you healed yourself.

## Keeping your Concentration buffs up

A Concentration buff lasts until you cancel it, and uses some of your concentration while it's up. `agent.self.concentration` is what you have left, out of `maxConcentration`. `agent.self.concentrationBuffs` lists the ones you keep up, with who wears each, as the concentration window shows them: a name longer than 14 characters is cut to its first 12 and "..", so Minor Bark Skin shows as "Minor Bark S..".

- They all end when you die (and when you log out): after you release, cast them again.
- Casting one that's already up does nothing new. Check `concentrationBuffs` first.
- `cast` fails with `refused` and "This spell requires 2 concentration points to cast!" in the log when you haven't enough left.
- Two buffs of the same kind don't add up. Cast Lesser Bark Skin while Minor Bark Skin is up and the server switches Minor Bark Skin off without ending it: it stays in your buff bar with `disabled`, and still uses its concentration. Cast only the best of each kind.

This casts every buff you have and don't keep up, the best of each kind:

```ts
const me = agent.self.objectId;
if (me === undefined) return "not in the world yet";

const cut = (name: string) => (name.length > 14 ? `${name.slice(0, 12)}..` : name);
const kind = (description: string) => description.replace(/\d+/g, "#");
const best = new Map<string, (typeof agent.self.skills)[number]>();

for (const spell of agent.self.skills.filter((skill) => skill.page === "Spells")) {
  if (!spell.details?.concentrationCost) continue;
  const other = best.get(kind(spell.details.description));
  if (!other || spell.level > other.level) best.set(kind(spell.details.description), spell);
}

const results: string[] = [];

for (const buff of best.values()) {
  if (agent.self.concentrationBuffs.some((kept) => kept.spell === cut(buff.name))) continue;

  const outcome = await cast(agent, buff.name, me);
  results.push(`${buff.name}: ${outcome.ok ? "cast" : outcome.reason}`);
}

return results.length ? results : "all up";
```

## Cancelling one

`cancelConcentration` cancels a buff you keep up, as a click on it in the concentration window does. Give the Spell's name, whole or cut, and `target` when the same buff is on two Characters. Cancel a buff that's switched off to get its concentration back:

```ts
const cut = (name: string) => (name.length > 14 ? `${name.slice(0, 12)}..` : name);
const off = agent.self.effects.filter((effect) => effect.disabled && agent.self.concentrationBuffs.some((kept) => kept.spell === cut(effect.name)));
const cancelled: string[] = [];

for (const effect of off) {
  const gone = await agent.sendAndWait({ name: "cancelConcentration", spell: effect.name }, () =>
    !agent.self.concentrationBuffs.some((kept) => kept.spell === cut(effect.name)), 5_000);
  cancelled.push(`${effect.name}: ${gone ? "cancelled" : "still up"}`);
}

return cancelled.length ? cancelled : "nothing switched off";
```

## Damage Spells

A damage Spell's `details.description` starts with "Inflicts", such as "Inflicts 5 heat damage to the target.", and its `target` is "ENEMY". A Magician's Gleam Ray, Gleam Streak and Gleam Blast hit at the end of a 2.6 s cast, as far as 1500 units away, with nothing to dodge or block. Prismatic Flare, at level 5, stuns a Mob for 3 s instead.

Face the Mob: casting at it starts the fight, and it comes at you. A bolt succeeds when it's launched, before it flies, so whether it hits shows only in the chat: "You hit the wild crouch for 5 damage!" or "You miss!". Of `cast`'s failures:

- `tooFar` beyond its range;
- `notInView` when it's behind you (face it, within 90° either side) or something's in the way;
- `noEffect` when it resists: "The wild crouch resists the effect! (12.0%)".

```ts
const me = agent.self.objectId;
const at = agent.self.position;
if (me === undefined || !at) return "not in the world yet";

const damage = agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .filter((spell) => spell.details?.description.startsWith("Inflicts"))
  .sort((a, b) => b.level - a.level)[0];
if (!damage) return "no damage Spell";

const mob = agent.surroundings.npcs.find((npc) => npc.realm === "None" && !npc.peaceful && npc.level <= agent.self.level);
if (!mob) return "no Mob in view";

const heading = (Math.round((Math.atan2(-(mob.position.x - at.x), mob.position.y - at.y) * 4096) / (2 * Math.PI)) + 4096) % 4096;
await agent.send({ name: "face", heading });
const outcome = await cast(agent, damage.name, mob.objectId);
return `${damage.name} at the ${mob.name}: ${outcome.ok ? "landed" : outcome.reason}`;
```

## Kiting

A Mob next to you keeps hitting you, and its hits interrupt your casts and stop you starting new ones. Kiting keeps it away: sprint away from it, turn round, and cast at it before it reaches you, again and again.

The numbers it rests on, from the server:

- You run at 191, or about 248 sprinting (`/sprint` turns sprint on and off). Sprinting costs endurance while you move.
- Mobs near the starting towns run at about 145 to 200, most at 180 to 200: as fast as you, unless you sprint. A Mob below a third of its health slows, down to a fifth of its speed near death.
- In a 2.6 s cast a Mob closes about 500 units. So start a cast only while it's further than that, and within the Spell's 1500.
- A Mob doesn't leash. It follows you up to 5,000 units, and gives up and walks home about 25 s after it was last in a fight.
- Moving interrupts a cast, so stop before you cast.

One round, for a Mob that's after you (its `targetId` is your `objectId`):

```ts
const me = agent.self.objectId;
const at = agent.self.position;
if (me === undefined || !at) return "not in the world yet";

const mob = agent.surroundings.npcs.find((npc) => npc.targetId === me);
if (!mob) return "nothing is after you";

// 900 units straight away from the Mob.
const dx = at.x - mob.position.x;
const dy = at.y - mob.position.y;
const length = Math.hypot(dx, dy) || 1;
const away = { x: Math.round(at.x + (dx / length) * 900), y: Math.round(at.y + (dy / length) * 900), z: at.z };

const sprinting = () => agent.self.effects.some((effect) => effect.name === "Sprint");
if (!sprinting()) await agent.sendAndWait({ name: "command", text: "/sprint" }, () => sprinting(), 2_000);
const ran = await runTo(agent, away);
if (sprinting()) await agent.sendAndWait({ name: "command", text: "/sprint" }, () => !sprinting(), 2_000);
if (!ran.ok) return `couldn't run away: ${ran.reason}`;

const now = agent.self.position ?? at;
const chaser = agent.surroundings.npcs.find((npc) => npc.objectId === mob.objectId);
if (!chaser) return "lost sight of it";

const gap = Math.hypot(chaser.position.x - now.x, chaser.position.y - now.y);
if (gap < 500) return `only ${Math.round(gap)} away: run again`;

const heading = (Math.round((Math.atan2(-(chaser.position.x - now.x), chaser.position.y - now.y) * 4096) / (2 * Math.PI)) + 4096) % 4096;
await agent.send({ name: "face", heading });
const outcome = await cast(agent, "Gleam Ray", chaser.objectId);
return `${Math.round(gap)} away: ${outcome.ok ? "landed" : outcome.reason}`;
```

You can also sprint with `useAbility` (see below): Sprint is an Ability, and using it turns sprint on or off, as `/sprint` does.

Prismatic Flare's 3 s stun buys you a cast without running. Your buffs from Way of the Moon last 20 minutes: cast them on yourself between fights, as a Naturalist keeps its Concentration buffs up.

## Abilities and Quick Cast

`useAbility` uses an Ability by its name in `agent.self.skills` (`page` "Abilities" or "RealmAbilities"), as a click on its icon does: Sprint, Quickcast, or a Realm Ability. Give the name exactly as `skills` has it: Quick Cast is "Quickcast" there, one word. One you don't have is refused (`queued: false`) with the reason. A passive one, such as Staves, does nothing.

An Eldritch, an Enchanter and a Mentalist get Quick Cast when they're promoted. It makes the next Spell you start within 3 s one that nothing interrupts, at twice the Power, and it lets you start that Spell while a hit's interrupt timer runs. It doesn't make the cast shorter. It has a 30 s Cooldown after a quickcasted Spell.

`cast` with `{ quickCast: true }` uses it for you: it turns your attack off (the server refuses Quick Cast while you attack), uses Quick Cast unless it's already up ("Quick Cast", two words, in `agent.self.effects`), and casts once it's up. It fails with `cooldown` while Quick Cast is on Cooldown, and with `noQuickCast` when it doesn't come up, as mid-cast: "You can't prepare a quickcast while casting a spell!".

It saves a fight once the Mob has reached you, when its hits would interrupt a normal cast or keep you from starting one. The Human's routine:

1. Cast at the Mob from range until it reaches you.
2. Quick Cast, and cast once more: the hits don't interrupt it.
3. If it's still alive, finish it with your staff, or kite if you're losing.

The Mob's first hit usually lands while you're still casting, and a cast past its halfway point carries on. Quick Cast is refused until that cast lands or breaks, so wait until `agent.self.casting` is gone first. Orla's first try failed that way, and Orla finished the fight with the staff at 16% health. Keep twice your damage Spell's Power back for the quickcasted one.

Steps 2 and 3, once the Mob is on you:

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.targetId === agent.self.objectId);
if (!mob) return "nothing is after you";

const damage = agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .filter((spell) => spell.details?.description.startsWith("Inflicts"))
  .sort((a, b) => b.level - a.level)[0];
if (!damage) return "no damage Spell";

// Let a cast in progress land or break first: Quick Cast is refused mid-cast.
if (agent.self.casting) await agent.waitFor(() => !agent.self.casting, 5_000);

const outcome = await cast(agent, damage.name, mob.objectId, { quickCast: true });
if (!agent.surroundings.npcs.some((npc) => npc.objectId === mob.objectId && npc.healthPercent > 0)) return `${outcome.ok ? "landed" : outcome.reason}, and it's dead`;

return await kill(agent, mob.objectId);
```
