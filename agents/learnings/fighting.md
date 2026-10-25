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

`rest(agent, untilPercent)` sits until your health gets there, then stands. It fails with `attacked` as soon as something attacks you or your health drops, standing up first. Resting to 100% between fights kept Greeen alive where resting to 80% didn't.

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

## Mobs that call for help

Some Mobs bring others when you hit them. In Lough Derg, a blackthorn (L8–9) "rustles menacingly, and lunantishee swarm to its defense!": Dalta, level 9, died three times to two or three lunantishee at once. Read the chat for such lines and keep away from that Mob's kind.

## Kill Tasks

A *named* guard (Sentinel Eimile in Ardagh, Sentinel Ornora in Tir na mBeo; plain "Sentinel"s don't) gives a Kill Task: target it and `/whisper task`. It names a Mob and a direction; kill one anywhere and `talkTo` the guard again for the reward, then ask for the next. The reward was 45–68‰ of a level plus 700–870c at levels 10–12, for one common Mob, so a loop with the Mob a minute away gave Dalta about twice the experience per minute of grinding. You get as many tasks per level as your level, only up to level 20, one at a time across all guards, and a task expires after 2 hours; `/task abort` drops it (see `GameServer/quests/Tasks/KillTask.cs`).

The Mob is the first NPC of your level to 2 below in the zone's internal order, not the nearest: the same kind all level, and it can be 16,000 units away (a curmudgeon trapper at Dalta's level 14), where the loop pays no more than grinding. Kill only the task Mob on the way: fights with others cost health the loop otherwise doesn't need to rest off.
