# Styles

A Style is a special melee attack you learn as a weapon spec rises: a Blademaster gets Shining Blade at Blades 2, Return Blade at 4, Taunting Blade at 6, and so on. Learned from how the server handles Styles; no Agent has used one in a fight yet.

## Which Styles you have

`agent.self.skills` lists them with `page === "Styles"`. Each has the spec level it needs as `level` and its spec as `line`. A new one appears when you train its spec to that level.

```ts
return agent.self.skills
  .filter((skill) => skill.page === "Styles")
  .map((style) => `${style.name} (${style.line} ${style.level})`);
```

## What a Style needs

Each Style in `agent.self.skills` has `details`, what its tooltip shows when a Human hovers its icon:

- `opening` and `openingResult` say what opens it. `Offensive` with `Any` needs nothing. `Offensive` with `Hit`, `Miss`, `Parry`, `Block` or `Evade` needs your last swing to have hit, missed, or been parried, blocked or evaded. `Offensive` with `Style` needs `openingStyle` to have landed just before. `Defensive` with `Parry`, `Block` or `Evade` needs the Mob's last attack on you to have been parried, blocked or evaded. `Positional` needs you at the Mob's `openingPosition` (`Back`, `Side` or `Front`), facing it.
- `followupStyles` are the Styles it opens, `fatigue` is its endurance cost, and `attackMod`, `defensiveMod` and `openingDamage` are its bonuses as the tooltip numbers them.

```ts
return agent.self.skills
  .filter((skill) => skill.page === "Styles" && skill.details)
  .map(({ name, details }) => `${name}: ${details!.opening} ${details!.openingResult}${details!.openingPosition ? ` ${details!.openingPosition}` : ""}${details!.openingStyle ? ` after ${details!.openingStyle}` : ""}, fatigue ${details!.fatigue}, opens ${details!.followupStyles.join(", ") || "nothing"}`);
```

## Using one

`useStyle` queues the Style for your next swing, as a click on its icon does. It doesn't swing by itself: you need a target and attack on, which `kill` gives you. The server answers in the chat:

- "You prepare to perform a Shining Blade!" when it's queued;
- "You perform your Shining Blade perfectly!" when the swing lands with it;
- "You fail to execute your Shining Blade perfectly!" when its opening wasn't there at the swing;
- "You are too fatigued to use this style!" without the endurance;
- "You must perform the Return Blade style before this one!" for a follow-up used too early.

Every Style costs endurance, which `agent.self.endurancePercent` shows and which comes back over time. Without enough, the server says you're too fatigued and your swings go on without the Style.

Each Style is used once, so queue it again for the next swing. This one fights with `kill` and queues Shining Blade again each time it's performed or failed:

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.realm === "None" && !npc.peaceful && npc.level === agent.self.level - 1);
if (!mob) return "no blue Mob in view";

let fighting = true;
const fight = kill(agent, mob.objectId).finally(() => { fighting = false; });
let styled = 0;

while (fighting) {
  await agent.send({ name: "useStyle", style: "Shining Blade" });
  const done = await agent.waitFor((percept) =>
    !fighting || (percept?.kind === "message" && /You (perform your|fail to execute your) Shining Blade/.test(percept.text) ? percept.text : undefined), 10_000);

  if (typeof done === "string" && done.startsWith("You perform")) styled++;
}

return { outcome: await fight, styled };
```

A `useStyle` the Harness refuses (`queued: false`) says why in `reason`: a Style you don't have, or none of that name in the `line` you gave.

## Reactive Styles

Return Blade opens only after the Mob attacks you and you parry. The server checks at your swing that the Mob's last attack was against you and that you parried it, so use the Style as soon as "... attacks you and you parry the blow!" arrives, before the Mob swings again. Your parry chance grows with your Parry spec.

```ts
const mob = agent.surroundings.npcs.find((npc) => npc.realm === "None" && !npc.peaceful && npc.level === agent.self.level - 1);
if (!mob) return "no blue Mob in view";

let fighting = true;
const fight = kill(agent, mob.objectId).finally(() => { fighting = false; });

while (fighting) {
  const parried = await agent.waitFor((percept) =>
    !fighting || (percept?.kind === "message" && /attacks you and you parry the blow/.test(percept.text)), 30_000);

  if (fighting && parried)
    await agent.send({ name: "useStyle", style: "Return Blade" });
}

return await fight;
```

The messages for the other openings: "... parries your attack!", "... blocks your attack!" and "... evades your attack!" when the Mob defends, and "... attacks you and you evade the blow!" or "... attacks you and you block the blow!" when you do. A positional Style needs you at the Mob's side or back, facing it.

## Two Styles with the same name

A Blademaster has two Slams, in Blunt and in Shields. Without `line`, `useStyle` takes the first Style of that name in `agent.self.skills`. Give the line to choose:

```ts
return await agent.send({ name: "useStyle", style: "Slam", line: "Shields" });
```
