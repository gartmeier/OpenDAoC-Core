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

`kill`'s 30 s without damage includes the run to the Mob: one more than about 5500 units away fails with `noDamage` before you get there. Run close with `runTo` first.

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

`rest(agent, untilPercent)` sits until your health gets there, and your Power too if you have any, then stands. It fails with `attacked` as soon as something attacks you or your health drops, standing up first. Resting to 100% between fights kept Greeen alive where resting to 80% didn't.

Dalta measured at level 10: no health comes back for about 20 s after a fight, then sitting heals about 0.9% a second, steadily, and running about 0.4%. So the time spent resting is set by how much health the fights cost, not by when you sit: compare Mobs by experience per % of health lost, which varied 2× between kinds of the same level.

At level 15 Dalta found the experience per % of health about the same, 0.5–0.6‰, for Mobs two levels below, at its level and one above, and only a third of that for a Mob two above (whose experience is capped at what one level above pays). So past the first levels, kills get cheaper mostly through better gear, and Mob drops at your level often beat the shops' 85%-quality gear: examine them.

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

## Praying at your Grave

When you release, the server says "A grave was erected where you were slain." Some of the experience you lost is in that Grave, and you win it back by praying there. You have only one Grave: if you die again before you pray, the new one replaces it, and what the old one held is gone.

- Your Grave is a World object named after you ("Dalta's Grave"). It's in `agent.surroundings.worldObjects` and the State summary once you're within 6000 units of where you died.
- Run within 2000 units of it, `target` it, stop, and type `/pray`. The server says "You begin your prayers!" and sits you down. About 5 s later the Grave leaves your Surroundings, and just after it the experience comes, with "You pray at your grave and gain back experience!": wait for the experience, not for the Grave to go.
- You get a Grave only if you lost experience: experience never drops below the start of your level, so a death at 0‰ leaves none.
- "You need to target a grave at which to pray!" means you didn't target it, and "You must be standing still to pray." that you were still running.

```ts
const grave = agent.surroundings.named("Dalta's Grave"); // your own Character's name
if (!grave)
  return "no Grave in view";

const ran = await runTo(agent, grave.position, 500);
if (!ran.ok)
  return ran;

await agent.send({ name: "target", objectId: grave.objectId });
const before = agent.self.levelPermill;
const gained = await agent.sendAndWait({ name: "command", text: "/pray" }, () => agent.self.levelPermill > before, 10_000);
return gained ? `prayed, now ${agent.self.levelPermill}‰` : "no experience back";
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

### Recalling

The Personal Bind Recall Stone in your backpack teleports you to your Bind point: after a kill far out, it saves you the run back. Examining it doesn't say so. Use it with `useItem` and its backpack slot, as a right-click on it does.

- It takes a 10 s cast, and you must stand still for all of it. Stop first, and don't move until it's done.
- It fails if you've been in combat in the last 10 s, so wait out the fight, then 10 s more, before you use it.
- It works once every 30 minutes.
- The server answers in the chat, in this order:
  - "You attempt to use the Personal Bind Recall Stone." on every use.
  - "You must wait 29 more minutes and 50 seconds to discharge this item!" (or "40 more seconds") on the reuse timer, or "You are already casting a spell." during another cast.
  - "You use the Personal Bind Recall Stone." past those, even on a use that then fails, so it doesn't mean the cast started.
  - "You must be standing still to use this item!" if you were moving, or "You have been in combat recently and cannot use this item!" within 10 s of a fight.
  - "You begin casting a Personal Recall Stone spell!": the cast started. "You move and interrupt your spellcast!" if you then move.
  - "You cast a Personal Recall Stone spell!" about 10 s later: it worked. A `jump` to your Bind point comes just after, even if you're already standing there, so wait for this line, not for your position to change.

```ts
const stone = agent.inventory.backpack.find(({ item }) => item.name === "Personal Bind Recall Stone");
if (!stone)
  return "no Recall Stone in the backpack";

// Stand still, and give the stop a moment to reach the server before the use does.
await agent.send({ name: "stop" });
await agent.waitFor(() => undefined, 1_000);

const reply = await agent.sendAndWait({ name: "useItem", slot: stone.slot }, (percept) =>
  percept?.kind === "message" && /You cast a Personal Recall Stone|standing still|interrupt your spellcast|in combat recently|must wait|already casting/.test(percept.text) ? percept.text : undefined, 15_000);
if (!reply?.startsWith("You cast"))
  return reply ?? "no answer within 15 s";

await agent.waitFor((percept) => percept?.kind === "jump", 2_000);
return `recalled to X:${agent.self.position?.x} Y:${agent.self.position?.y}`;
```

## Training

Each level gives you specialization points (`agent.self.specPoints`). Raising a line to level n costs n points. `train(agent, line, level)` types `/train` as a Human would, and fails with `notTrained` without enough points. `agent.self.specs` has your lines and their levels, named as `/train` takes them, which isn't always what the client shows: "Light" for Light Magic, "Shields" for Shield.

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

## Mobs that call for help

Some Mobs bring others when you hit them. In Lough Derg, a blackthorn (L8–9) "rustles menacingly, and lunantishee swarm to its defense!": Dalta, level 9, died three times to two or three lunantishee at once. Read the chat for such lines and keep away from that Mob's kind.
