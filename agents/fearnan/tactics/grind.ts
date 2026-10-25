import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";

/** Heals itself with its best heal while below `below`% and it has the Power. */
export async function healSelf(agent: Agent, below = 70): Promise<void> {
  const me = agent.self.objectId;
  if (me === undefined) return;

  const heal = agent.self.skills
    .filter((skill) => skill.page === "Spells" && skill.details?.description.startsWith("Heals"))
    .sort((a, b) => b.level - a.level)[0];
  if (!heal) return;

  await agent.send({ name: "target", objectId: me });

  while (agent.self.healthPercent < below && agent.self.power >= (heal.details?.powerCost ?? 0)) {
    const reply = await agent.sendAndWait({ name: "castSpell", spell: heal.name }, (percept) =>
      percept?.kind === "message" && /You cast a|interrupt|must wait|enough power|can't cast|select a target/.test(percept.text) ? percept.text : undefined, 10_000);
    if (!reply?.startsWith("You cast")) return;
  }
}

/** Fights back whoever attacks it. */
async function fightBack(agent: Agent): Promise<Outcome | undefined> {
  const me = agent.self.objectId;
  const attacker = me === undefined ? undefined : agent.surroundings.attacking(me)[0];
  return attacker ? await kill(agent, attacker.objectId) : undefined;
}

/**
 * Kills Mobs of `minLevel` to `maxLevel` levels below its own (0 = its level) for `minutes`, nearest first,
 * healing and resting to full between fights, until it reaches `stopAtLevel`.
 */
export async function grind(agent: Agent, minutes: number, stopAtLevel: number, below = [1, 0], avoid: string[] = []): Promise<Outcome & { kills?: number; deaths?: number }> {
  const end = agent.clock.now() + minutes * 60_000;
  let kills = 0;
  let deaths = 0;
  const shunned = new Map<number, number>();

  while (agent.clock.now() < end && agent.self.level < stopAtLevel) {
    if (agent.self.dead) {
      deaths++;
      await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
      continue;
    }

    const fought = await fightBack(agent);
    if (fought) {
      if (fought.ok) kills++;
      continue;
    }

    if (agent.self.healthPercent < 100 || agent.self.powerPercent < 100) {
      await healSelf(agent);
      const rested = await rest(agent, 100);
      if (!rested.ok && rested.reason === "attacked") continue;
    }

    const level = agent.self.level;
    const at = agent.self.position;
    if (!at) return failed("noPosition");

    const mob = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && npc.healthPercent > 0 && !avoid.includes(npc.name)
        && npc.level >= level - Math.max(...below) && npc.level <= level - Math.min(...below) && (shunned.get(npc.objectId) ?? 0) < 2)
      .map((npc) => ({ npc, away: Math.hypot(npc.position.x - at.x, npc.position.y - at.y) }))
      .sort((a, b) => a.away - b.away)[0];

    if (!mob) return { ...failed("noMob"), kills, deaths };

    const outcome = await kill(agent, mob.npc.objectId);
    if (outcome.ok) kills++;
    else if (outcome.reason === "died") deaths++;
    else shunned.set(mob.npc.objectId, (shunned.get(mob.npc.objectId) ?? 0) + 1);
  }

  return { ...succeeded, kills, deaths };
}
