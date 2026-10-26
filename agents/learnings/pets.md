# Pets

A Pet is a creature you summon and command, which fights for you until it dies or you release it. A Druid summons one from Nature: Call Minor Nature Spirit at Nature 1, Call Lesser Nature Spirit at 7, Call Nature Spirit at 12. Learned from how the server handles pets; no Agent has fought with one yet.

## Summoning it

Summoning is a Spell like any other: `castSpell` it, no target needed. It takes 6 s and a quarter of your Power. The Pet appears just in front of you, as one of a wolf cub, a lynx cub, a bear cub or a minor nature spirit, at random. Its level is three quarters of yours, rounded down: 3 at level 5, 5 at level 7.

- The server says "The wolf cub is now under your control." and opens the pet window, which `agent.self.pet` shows.
- With a Pet already out it says "You already have a charmed creature, release it first!".
- `agent.self.pet` goes back to `undefined` when the Pet dies, when you release it, when you die, when you change zone, and when you get more than 10,000 units away from it. Run this again then:

```ts
if (agent.self.pet) return `already have #${agent.self.pet.objectId}`;

const summoning = agent.self.skills.filter((skill) => skill.page === "Spells").find((spell) => spell.details?.description.startsWith("Summon a pet"));
if (!summoning) return "no summoning Spell";

const reply = await agent.sendAndWait({ name: "castSpell", spell: summoning.name }, (percept) =>
  percept?.kind === "petWindow" && percept.action === "Open" ? "summoned" :
  percept?.kind === "message" && /interrupt|must wait|enough power|can't cast|release it first/.test(percept.text) ? percept.text : undefined, 10_000);
return reply ?? "no answer within 10 s";
```

## Finding it

`agent.self.pet` has the Pet's `objectId`, `aggression` and `walk`. The Pet itself is an NPC in your Surroundings, with its health and what it's fighting. Every Pet there has `pet: true`, other players' too, so find yours by its object ID:

```ts
const pet = agent.self.pet && agent.surroundings.npc(agent.self.pet.objectId);
return pet ? `${pet.name} level ${pet.level}, ${pet.healthPercent}% health, fighting #${pet.targetId}, ${agent.self.pet?.aggression} and ${agent.self.pet?.walk}` : "no pet";
```

## Commanding it

`commandPet` clicks one button in the pet window. It has three rows, and an order changes only its own row, leaving the others as they were:

- **Aggression:** `Aggressive` attacks any Mob that comes within 512 units. `Defensive`, as it starts, fights only what attacks you or it. `Passive` never fights, even when hit.
- **Walk:** `Follow` you, as it starts. `Stay` where it is. `GoTarget` goes to what you target, within 1250 units. `ComeHere` comes back to you. Each makes it break off its fight first, and makes an aggressive Pet that's fighting defensive.
- **Commands:** `Attack` sends it at what you target, within 2000 units, and makes a passive pet defensive. `Release` lets it go.

The server answers each order, as "You command the wolf cub to kill your target!" or "Your target is too far away to attack with your pet!". The Harness sends an order even with no pet out.

## Fighting with it

The Pet doesn't join your fights by itself. It defends you when something hits you, and fights back when hit, but a Mob you attack it leaves alone until you send it in with `Attack`.

The damage the Pet does angers the Mob at you too, at 30% of what it angers it at the Pet. So a Mob the Pet fights may turn on you: stay back and heal the Pet or yourself between its blows, rather than meleeing beside it.

This sends the Pet at a Mob and waits until the Mob dies or the Pet is gone:

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.name === "sand crab");
if (!mob) return "no sand crab in view";
if (!agent.self.pet) return "no pet";

await agent.send({ name: "target", objectId: mob.objectId });
const ordered = await agent.sendAndWait({ name: "commandPet", order: "Attack" }, (percept) =>
  percept?.kind === "message" && /You command|too far away/.test(percept.text) ? percept.text : undefined, 5_000);
if (!ordered?.startsWith("You command")) return ordered ?? "no answer";

const ended = await agent.waitFor(() => {
  const target = agent.surroundings.npc(mob.objectId);
  if (!target || target.healthPercent === 0) return "the Mob died";
  if (!agent.self.pet) return "the Pet is gone";
  if (agent.self.dead) return "I died";
}, 120_000);
return ended ?? "still fighting after 2 minutes";
```
