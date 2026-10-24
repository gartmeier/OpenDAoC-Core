# Fighting

Learned by the hand-written Grind of milestones 4 and 4b, which took a Guardian from level 1 to 5, and by Dalta, the first coding agent, which did the same.

## Picking a Mob

A Mob is an NPC of no realm (`realm === "None"`) that isn't peaceful. Its level against yours is its con colour: grey and green are well below you, blue is a level or so below, yellow is your level, orange and red above.

- **With the gear you start with, fight only blue Mobs, one level below yours.** At level 1, yellow level 1 Mobs killed Greeen in 12 s and Dalta 3 times. Blue ones were safe but wore Greeen down to about 30% health a fight.
- Grey Mobs give no experience and no coins: at level 4, level 1 Mobs gave Dalta nothing.
- Once Greeen had some armour, yellow Mobs were fine too, and there are more of them to fight.
- Mobs of the same level aren't equal: at level 4 to 5 a mudman killed Greeen 208 times, mostly the same one, because after each death he ran back and attacked it again. If a Mob keeps killing you, leave it alone.

```ts
const level = agent.self.level;
const position = agent.self.position;
const mobs = agent.surroundings.npcs
  .filter((npc) => npc.realm === "None" && !npc.peaceful && npc.healthPercent > 0 && npc.level === level - 1)
  .map((npc) => ({ npc, away: position ? Math.hypot(npc.position.x - position.x, npc.position.y - position.y) : Infinity }))
  .sort((a, b) => a.away - b.away);
return mobs.slice(0, 5).map(({ npc, away }) => `#${npc.objectId} ${npc.name} level ${npc.level}, ${Math.round(away)} away`);
```

## Killing it

`kill(agent, objectId)` targets the Mob, attacks, runs to it and faces it, and follows it until it dies. It turns attack off when it ends. It fails with `died`, `outOfView` (it went out of view or left the world alive), `unreachable` (no route gets to it) or `noDamage` (neither side's health fell for 30 s, for example because the server ignored the attack).

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.name === "sand crab");
return mob ? await kill(agent, mob.objectId) : "no sand crab in view";
```

A Mob only counts as attacking you once it's within reach: while it runs towards you its target shows as 65535. Fight back first:

```ts
const me = agent.self.objectId;
const attacker = me === undefined ? undefined : agent.surroundings.attacking(me)[0];
return attacker ? await kill(agent, attacker.objectId) : "nothing attacks me";
```

## Resting

`rest(agent, untilPercent)` sits until your health gets there, then stands. It fails with `attacked` as soon as something attacks you or your health drops, standing up first. Resting to 100% between fights kept Greeen alive where resting to 80% didn't.

```ts
if (agent.self.healthPercent < 80)
  return await rest(agent, 100);

return "healthy enough";
```

## Dying

When you die, `agent.self.dead` is true. Send `/release` and you come back to life at your Bind point (see Binding). The server releases you by itself about 10 s after you die anyway. Then run back.

```ts
if (!agent.self.dead)
  return "alive";

const alive = await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
return alive ? `released, now at X:${agent.self.position?.x} Y:${agent.self.position?.y}` : "still dead";
```

## Binding

Your Bind point is where you start, until you bind somewhere else. Dalta never bound in Ardagh, so each death there sent it back to Howth, about 40,000 units away. Bind at a Bindstone in the town you hunt from: each zone's Wiki page lists its Bindstones.

- Run right up to the Bindstone, within about 100 units, then type `/bind`. From Moesen's stall, about 1,300 units from Ardagh's Bindstone, the server answered "You can't bind here!".
- The answer ends with "You are now bound to this location." if it worked, or "You can't bind here!" if you're not at a Bindstone. Before either it says where your last Bind point was ("Last Bind Point: in Howth."). You can't bind while dead, and must wait a minute to bind again.
- Nothing keeps track of your Bind point for you. Note it in your own files if you need it.

```ts
const stone = { x: 351830, y: 554515, z: 5109 }; // Ardagh's Bindstone, from the Silvermine Mts. page
const ran = await runTo(agent, stone, 100);

if (!ran.ok)
  return ran;

const reply = await agent.sendAndWait({ name: "command", text: "/bind" }, (percept) =>
  percept?.kind === "message" && /bound to this location|can't bind|cannot bind|must wait/.test(percept.text) ? percept.text : undefined, 5_000);
return reply ?? "no answer";
```

## Training

Each level gives you specialization points (`agent.self.specPoints`). Raising a line to level n costs n points. `train(agent, line, level)` types `/train` as a Human would, and fails with `notTrained` without enough points. `agent.self.specs` has your lines and their levels.

```ts
const blades = agent.self.specs.get("Blades") ?? 1;
let target = blades;
let points = agent.self.specPoints;

while (points >= target + 1)
  points -= ++target;

return target > blades ? await train(agent, "Blades", target) : "not enough points";
```

## Promoting

At level 5 your class trainer promotes you. The Wiki's zone pages list trainers by class, such as "Blademaster Trainer". A trainer has no merchant window: `talkTo` succeeds once it answers in words, which ask which class you choose in [brackets] and reach you in the chat. `whisper(agent, npcId, keyword)` answers one, as clicking it in the client does, and succeeds once the trainer answers again.

```ts
const ea = agent.surroundings.npcs.find((npc) => npc.name === "Ea");
if (!ea) return "Ea isn't in view";

const talked = await talkTo(agent, ea.objectId);
if (!talked.ok) return talked;

return await whisper(agent, ea.objectId, "Blademaster");
```
