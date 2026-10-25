# Spells

A Spell is magic you cast from one of your spell lines. A Naturalist gets heals from Regrowth and Concentration buffs from Nurture: Minor Revigoration at level 1, Minor Bark Skin at 2, Strength of the Oak and Minor Renewal at 3, Lesser Bark Skin and Renewal at 4, Oak's Stoutness and Minor Revivification at 5. Learned from how the server handles Spells; no Agent has cast one in a fight yet.

## Which Spells you have

`agent.self.skills` lists them with `page === "Spells"`. Each has `spellDetails`, what its tooltip shows when a Human hovers its icon: what it does, its `powerCost` or `concentrationCost`, `castingSeconds` and `range`. A new one appears as you level, and a better heal of the same kind takes the place of the one before.

```ts
return agent.self.skills
  .filter((skill) => skill.page === "Spells")
  .map(({ name, spellDetails: d }) => `${name}: ${d?.description} ${d?.powerCost ? `${d.powerCost} power` : `${d?.concentrationCost ?? 0} concentration`}, ${d?.castingSeconds} s, range ${d?.range}`);
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

const heal = agent.self.skills.find((skill) => skill.page === "Spells" && skill.spellDetails?.description.startsWith("Heals"));
if (!heal) return "no heal";

await agent.send({ name: "target", objectId: me });

while (agent.self.healthPercent < 90 && agent.self.power >= (heal.spellDetails?.powerCost ?? 0)) {
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

for (const skill of agent.self.skills) {
  const details = skill.spellDetails;
  if (skill.page !== "Spells" || !details?.concentrationCost) continue;
  const other = best.get(kind(details.description));
  if (!other || skill.level > other.level) best.set(kind(details.description), skill);
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
