# Moving

Learned in milestones 3 and 3b, and while shopping in milestone 4b.

## Running somewhere

`runTo(agent, { x, y, z }, within?)` runs along a route on the zone's navmesh and succeeds once you arrive, `within` units short of the end if you give it. It fails with why the run stopped, such as `unreachable` or `movedByServer`, or with `timeout` after 5 minutes.

- Positions are global, as `/gloc` gives them, and as the Wiki lists them.
- If you don't know the ground's height, give your own: the route ends at the nearest point you can walk to.
- A target out of reach still gets a route as far as it goes, and the run ends short of it.

```ts
const here = agent.self.position;
if (!here) return "I don't know where I am";

return await runTo(agent, { x: here.x + 500, y: here.y, z: here.z });
```

## Running faster

Towns have Hasteners (`wiki/` lists them by zone; Silvermine's is Filidh Medyr). `talkTo` one and it casts a speed buff on you and everyone near at once: "Your speed greatly increases!". Dalta's `agent.self.maxSpeed` went from 191 to 281. It lasts 10 minutes, but a fight ends it, so take it for a long run, not for a grind. The Mind doesn't see buffs (the buff bar comes only as a bare `SendUpdateIcons`), so `agent.self.maxSpeed` is how to tell it's on.

```ts
const hastener = agent.surroundings.npcs.find((npc) => npc.name === "Filidh Medyr");
if (!hastener) return "no Hastener in view";
await talkTo(agent, hastener.objectId);
return await agent.waitFor(() => agent.self.maxSpeed > 191, 5_000) ? agent.self.maxSpeed : "no buff";
```

## Doors and buildings

Routes go through doors a Character opens by clicking, such as a town building's, even while they're closed: the server lets you through. Where the navmesh leaves a short gap at a door, as at Eira's hut in Connla with its raised floor, the route steps across it.

## What you see

You see objects within 6000 units. Your Surroundings forget an object once you've moved more than that away from it, or when the server says it went out of view or left the world. So what's in `agent.surroundings` is what's around you now, give or take its last update.

```ts
const here = agent.self.position;
if (!here) return "I don't know where I am";

return agent.surroundings.objects.map((object) => `${object.kind} ${object.name} ${Math.round(Math.hypot(object.position.x - here.x, object.position.y - here.y))} away`);
```

## Don't teleport

The server checks how fast you move. Only ever move by running, with `runTo` or the `moveTo` Primitive.
