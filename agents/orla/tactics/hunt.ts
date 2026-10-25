import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";

type Point = { x: number; y: number; z?: number };

const distance = (agent: Agent, p: Point) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

const headingTo = (agent: Agent, p: Point) => {
  const at = agent.self.position!;
  return (Math.round((Math.atan2(-(p.x - at.x), p.y - at.y) * 4096) / (2 * Math.PI)) + 4096) % 4096;
};

/** The best damage Spell you have, if any. */
const damageSpell = (agent: Agent) =>
  agent.self.skills
    .filter((skill) => skill.page === "Spells" && skill.details?.description.startsWith("Inflicts"))
    .sort((a, b) => b.level - a.level)[0];

/**
 * Stands at about the Spell's range from the Mob and casts at it until it first hits you or the Power runs out,
 * then fights it with the staff.
 */
export async function pull(agent: Agent, objectId: number): Promise<Outcome> {
  const spell = damageSpell(agent);
  const mob = () => agent.surroundings.npcs.find((npc) => npc.objectId === objectId);
  const first = mob();
  if (!first) return failed("outOfView");

  if (spell) {
    const range = (spell.details?.range ?? 1500) - 150;
    const away = distance(agent, first.position);
    if (away > range) {
      const ran = await runTo(agent, first.position, range);
      if (!ran.ok) return ran;
    } else if (away < 800 && first.targetId !== agent.self.objectId) {
      // Too close for more than one cast: back off from it first, while it doesn't care about me yet.
      const at = agent.self.position!;
      const length = away || 1;
      const back = range - away;
      await runTo(agent, { x: Math.round(at.x + ((at.x - first.position.x) / length) * back), y: Math.round(at.y + ((at.y - first.position.y) / length) * back), z: at.z });
    }

    let hit = false;
    const onHit = (text: string) => / hits your | attacks you /.test(text);

    while (!hit && agent.self.power >= (spell.details?.powerCost ?? 0) && !agent.self.dead) {
      const target = mob();
      if (!target || target.healthPercent <= 0) return target ? succeeded : failed("outOfView");
      if (distance(agent, target.position) < 250) break;

      await agent.send({ name: "face", heading: headingTo(agent, target.position) });
      await agent.send({ name: "target", objectId });
      const reply = await agent.sendAndWait({ name: "castSpell", spell: spell.name }, (percept) =>
        percept?.kind === "message" && (onHit(percept.text) || /You hit|resists|interrupt|too far away|not visible|can't see|must wait|enough power|can't cast/.test(percept.text)) ? percept.text : undefined, 10_000);

      if (!reply) break;
      if (onHit(reply) || /interrupt/.test(reply)) hit = true;
      else if (/too far away/.test(reply)) await runTo(agent, target.position, range - 300);
      else if (/must wait/.test(reply)) await agent.waitFor(() => undefined, 1_000);
      else if (/enough power|can't see|not visible|can't cast/.test(reply)) break;
    }

    const after = mob();
    if (!after || after.healthPercent <= 0) return succeeded;
  }

  return await kill(agent, objectId);
}

/** Casts the best armour buff on yourself if it isn't up, then rests the Power back. */
export async function buffUp(agent: Agent): Promise<void> {
  const me = agent.self.objectId;
  const ward = agent.self.skills
    .filter((skill) => skill.page === "Spells" && skill.details?.description.startsWith("Increases your armor factor"))
    .sort((a, b) => b.level - a.level)[0];
  if (me === undefined || !ward || agent.self.effects.some((effect) => effect.name === ward.name)) return;

  await agent.send({ name: "target", objectId: me });
  await agent.sendAndWait({ name: "castSpell", spell: ward.name }, (percept) =>
    percept?.kind === "message" && /You cast a|interrupt|must wait|enough power|can't cast|select a target/.test(percept.text) ? percept.text : undefined, 10_000);
  await rest(agent, 100);
}

/**
 * Pulls the nearest Mob of a level in `levels` with `pull`, resting to full between fights and fighting back first,
 * until `untilLevel` or `minutes` pass. Avoids Mob kinds in `avoid`.
 */
export async function hunt(agent: Agent, levels: (level: number) => number[], untilLevel: number, minutes: number, avoid: string[] = []): Promise<Outcome> {
  const end = Date.now() + minutes * 60_000;
  const log: string[] = [];
  let kills = 0;
  let deaths = 0;

  while (Date.now() < end && agent.self.level < untilLevel) {
    if (agent.self.dead) {
      deaths++;
      await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
      log.push(`died at level ${agent.self.level}`);
      if (deaths >= 3) break;
      // Surroundings from before the release are stale: let them settle before trusting them.
      await agent.waitFor(() => undefined, 3_000);
      continue;
    }

    const me = agent.self.objectId;
    // A Mob after me, in reach or still on its way: meet it with Spells too, not only the staff.
    const attacker = me === undefined ? undefined : (agent.surroundings.attacking(me).find((npc) => distance(agent, npc.position) < 500)
      ?? agent.surroundings.npcs.find((npc) => npc.targetId === me && npc.healthPercent > 0 && npc.realm === "None" && distance(agent, npc.position) < 2000));
    if (attacker) {
      const k = await pull(agent, attacker.objectId);
      if (k.ok) kills++;
      else log.push(`fought back ${attacker.name} L${attacker.level}: ${k.reason}`);
      continue;
    }

    if (agent.self.healthPercent < 95 || agent.self.powerPercent < 95) {
      await rest(agent, 100);
      continue;
    }

    await buffUp(agent);

    const wanted = levels(agent.self.level);
    const mob = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && npc.healthPercent > 0 && wanted.includes(npc.level) && !avoid.includes(npc.name))
      .sort((a, b) => distance(agent, a.position) - distance(agent, b.position))[0];
    if (!mob) {
      log.push("no Mob in view");
      break;
    }

    const k = await pull(agent, mob.objectId);
    if (k.ok) kills++;
    else log.push(`${mob.name} L${mob.level}: ${k.reason}`);
  }

  console.log(`kills ${kills}, deaths ${deaths}, level ${agent.self.level} ${agent.self.levelPermill}‰\n${log.join("\n")}`);
  return deaths >= 3 ? failed("diedThreeTimes") : succeeded;
}
