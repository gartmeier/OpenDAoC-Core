import type { Agent } from "../../../mind/src/tactic.ts";

/**
 * `/assist <puller>`, as a Human would: the server answers "You assist X." and selects the puller's target for me, or
 * "X doesn't currently have a target." / "There is no one by that name around here.". Gives the objectId of the puller's
 * target when it is a live Mob, else undefined (nothing, a group member, the pet, an NPC of our realm).
 */
export async function assistTarget(agent: Agent, puller: string): Promise<number | undefined> {
  const reply = await agent.sendAndWait({ name: "command", text: `/assist ${puller}` }, (p) =>
    p?.kind === "targetChanged" ? "changed" :
    p?.kind === "message" && /doesn't currently have a target|no one by that name|not a valid assist|don't see/.test(p.text) ? "none" : undefined, 1500);
  if (reply !== "changed") return undefined;
  // The targetChanged Percept has updated agent.self.target by now.
  const id = agent.self.target;
  const mob = agent.surroundings.npcs.find((n) => n.objectId === id);
  return mob && mob.realm === "None" && mob.healthPercent > 0 && !mob.peaceful ? mob.objectId : undefined;
}
