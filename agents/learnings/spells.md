# Spells

A Spell is magic you cast from one of your spell lines. A Naturalist gets heals from Regrowth and Concentration buffs from Nurture: Minor Revigoration at level 1, Minor Bark Skin at 2, Strength of the Oak and Minor Renewal at 3, Lesser Bark Skin and Renewal at 4, Oak's Stoutness and Minor Revivification at 5. A Magician gets damage from Way of the Sun and buffs on itself from Way of the Moon: Gleam Ray and Lesser Powerward at 1, Gleam Streak and Aura of Turning at 2, Gleam Blast and Powerward at 3, Greater Aura of Turning at 4, Prismatic Flare and Lesser Powerbarrier at 5. Learned from how the server handles Spells; no Agent has cast one in a fight yet.

## Which Spells you have

`agent.self.skills` lists them with `page === "Spells"`. Each has `details`, what its tooltip shows when a Human hovers its icon (a Style's `details` are a different shape, so check `page` first): what it does, its `powerCost` or `concentrationCost`, `castingSeconds` and `range`. A new one appears as you level, and a better heal of the same kind takes the place of the one before.

```ts
return agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .map(({ name, details: d }) => `${name}: ${d?.description} ${d?.powerCost ? `${d.powerCost} power` : `${d?.concentrationCost ?? 0} concentration`}, ${d?.castingSeconds} s, range ${d?.range}`);
```

## Casting one

`castSpell` casts the Spell at what you target, as a click on its icon does. A heal or a buff needs a friendly target, and yourself counts: `target` your own `agent.self.objectId` first. With nothing targeted the server says "You must select a target for this spell!", and with a Mob targeted the cast fails without a word.

Stand still and stand up: moving interrupts the cast, and you can't cast sitting. The server answers in the chat:

- "You begin casting a Minor Revigoration spell!" when the cast starts;
- "You cast a Minor Revigoration spell!" when it lands, its casting time later;
- "You move and interrupt your spellcast!" if you moved, or "... attacks you and your spell is interrupted!" if something hit you;
- "You must wait 3 seconds to cast a spell!" just after you swung or were hit;
- "You don't have enough power to cast that!", "You can't cast while sitting!" or "You must select a target for this spell!" when it can't start.

```ts
const me = agent.self.objectId;
if (me === undefined) return "not in the world yet";

await agent.send({ name: "target", objectId: me });
const reply = await agent.sendAndWait({ name: "castSpell", spell: "Minor Revigoration" }, (percept) =>
  percept?.kind === "message" && /You cast a|interrupt|must wait|enough power|can't cast|select a target|already casting/.test(percept.text) ? percept.text : undefined, 10_000);
return reply ?? "no answer within 10 s";
```

A `castSpell` the Harness refuses (`queued: false`) says why in `reason`: a Spell you don't have. Style names don't work, nor Spell names with `useStyle`.

## Healing between fights

Heal between fights, not in them: every hit on you interrupts the cast, every swing of yours holds the next cast off for a few seconds, and casting turns your attack off. Heals cost Power, which `agent.self.power` and `powerPercent` show. `rest` sits until both your health and your Power are back.

```ts
const me = agent.self.objectId;
if (me === undefined) return "not in the world yet";

const heal = agent.self.skills.filter((skill) => skill.page === "Spells").find((spell) => spell.details?.description.startsWith("Heals"));
if (!heal) return "no heal";

await agent.send({ name: "target", objectId: me });

while (agent.self.healthPercent < 90 && agent.self.power >= (heal.details?.powerCost ?? 0)) {
  const reply = await agent.sendAndWait({ name: "castSpell", spell: heal.name }, (percept) =>
    percept?.kind === "message" && /You cast a|interrupt|must wait|enough power|can't cast/.test(percept.text) ? percept.text : undefined, 10_000);

  if (!reply?.startsWith("You cast")) return reply ?? "no answer";
}

return await rest(agent, 100);
```

`kill` targets the Mob again itself, so nothing needs undoing after you healed yourself.

## Keeping your Concentration buffs up

A Concentration buff lasts until you cancel it, and uses some of your concentration while it's up. `agent.self.concentration` is what you have left, out of `maxConcentration`. `agent.self.concentrationBuffs` lists the ones you keep up, with who wears each, as the concentration window shows them: a name longer than 14 characters is cut to its first 12 and "..", so Minor Bark Skin shows as "Minor Bark S..".

- They all end when you die (and when you log out): after you release, cast them again.
- Casting one that's already up does nothing new. Check `concentrationBuffs` first.
- "This spell requires 2 concentration points to cast!" means you haven't enough left.
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

await agent.send({ name: "target", objectId: me });
const cast: string[] = [];

for (const buff of best.values()) {
  if (agent.self.concentrationBuffs.some((kept) => kept.spell === cut(buff.name))) continue;

  const reply = await agent.sendAndWait({ name: "castSpell", spell: buff.name }, (percept) =>
    percept?.kind === "message" && /You cast a|interrupt|must wait|requires|can't cast/.test(percept.text) ? percept.text : undefined, 10_000);
  cast.push(`${buff.name}: ${reply ?? "no answer"}`);
}

return cast.length ? cast : "all up";
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

Target the Mob, face it and stand still. Casting at a Mob starts the fight: it comes at you. The server answers in the chat:

- "You begin casting a Gleam Ray spell!" when the cast starts;
- "You hit the wild crouch for 5 damage!" when it lands;
- "The wild crouch resists the effect! (12.0%)" when it doesn't;
- "You must select a target for this spell!" with nothing targeted;
- "That target is too far away!" beyond its range;
- "Your target is not visible!" when it's behind you: you must face it, within 90° either side;
- "You can't see your target from here!" when something's in the way;
- the interrupt, "must wait", Power and sitting lines, as for a heal.

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
await agent.send({ name: "target", objectId: mob.objectId });

const reply = await agent.sendAndWait({ name: "castSpell", spell: damage.name }, (percept) =>
  percept?.kind === "message" && /You hit|resists|interrupt|too far away|not visible|can't see|must wait|enough power|can't cast/.test(percept.text) ? percept.text : undefined, 10_000);
return `${damage.name} at the ${mob.name}: ${reply ?? "no answer within 10 s"}`;
```

## Kiting

A Mob next to you hits you, and every hit interrupts your cast. Kiting keeps it away: sprint away from it, turn round, and cast at it before it reaches you, again and again.

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
await agent.send({ name: "target", objectId: chaser.objectId });

const reply = await agent.sendAndWait({ name: "castSpell", spell: "Gleam Ray" }, (percept) =>
  percept?.kind === "message" && /You hit|resists|interrupt|too far away|not visible|can't see|must wait|enough power/.test(percept.text) ? percept.text : undefined, 10_000);
return `${Math.round(gap)} away: ${reply ?? "no answer within 10 s"}`;
```

You can also sprint with `useAbility` (see below): Sprint is an Ability, and using it turns sprint on or off, as `/sprint` does.

Prismatic Flare's 3 s stun buys you a cast without running. Your buffs from Way of the Moon last 20 minutes: cast them on yourself between fights, as a Naturalist keeps its Concentration buffs up.

## Abilities and Quick Cast

`useAbility` uses an Ability by its name in `agent.self.skills` (`page` "Abilities" or "RealmAbilities"), as a click on its icon does: Sprint, Quick Cast, or a Realm Ability. One you don't have is refused (`queued: false`) with the reason. A passive one, such as Staves, does nothing.

An Eldritch, an Enchanter and a Mentalist get Quick Cast when they're promoted. It makes the next Spell you start within 3 s one that nothing interrupts, at twice the Power. It doesn't make the cast shorter. The server says:

- "You have activated quickcast." when it's ready, and "Quick Cast" is in `agent.self.effects` until you cast or the 3 s run out, which the server doesn't announce;
- "Your next spell will not be quickcasted." when you use it again to turn it off;
- "You can not cast when your are in melee combat." when your attack is on: turn it off first;
- "You must wait 12 more second to attempt to quick cast!" for 30 s after a quickcasted Spell;
- "You can't prepare a quickcast while casting a spell!" mid-cast.

It saves a fight once the Mob has reached you, when every hit would interrupt a normal cast. The Human's routine:

1. Cast at the Mob from range until it reaches you.
2. Quick Cast, and cast once more: the hits don't interrupt it.
3. If it's still alive, finish it with your staff, or kite if you're losing.

Steps 2 and 3, once the Mob is on you:

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.targetId === agent.self.objectId);
if (!mob) return "nothing is after you";

const damage = agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .filter((spell) => spell.details?.description.startsWith("Inflicts"))
  .sort((a, b) => b.level - a.level)[0];
if (!damage) return "no damage Spell";

await agent.send({ name: "attack", on: false });
await agent.send({ name: "target", objectId: mob.objectId });

const ready = await agent.sendAndWait({ name: "useAbility", ability: "Quick Cast" }, (percept) =>
  percept?.kind === "message" && /activated quickcast|must wait|melee combat|while casting/.test(percept.text) ? percept.text : undefined, 3_000);
if (!ready?.includes("activated")) return `no Quick Cast: ${ready ?? "no answer"}`;

const reply = await agent.sendAndWait({ name: "castSpell", spell: damage.name }, (percept) =>
  percept?.kind === "message" && /You hit|resists|too far away|not visible|can't see|enough power/.test(percept.text) ? percept.text : undefined, 10_000);
if (!agent.surroundings.npcs.some((npc) => npc.objectId === mob.objectId && npc.healthPercent > 0)) return `${reply}, and it's dead`;

return await kill(agent, mob.objectId);
```
