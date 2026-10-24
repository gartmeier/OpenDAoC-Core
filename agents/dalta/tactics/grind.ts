import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { train } from "../../../mind/src/tactics/train.ts";

/** Spends spec points on Blades. */
async function trainBlades(agent: Agent): Promise<void> {
  const blades = agent.self.specs.get("Blades") ?? 1;
  let target = blades;
  let points = agent.self.specPoints;
  while (points >= target + 1) points -= ++target;
  if (target > blades) await train(agent, "Blades", target);
}

/** Kills blue and yellow Mobs nearest first until `goal` level, resting between fights. */
export async function grind(agent: Agent, goal: number, log: string[] = [], knownLevel = 0, spots: { x: number; y: number; z: number }[] = [], minutes = 25, band: [number, number] = [-1, -1]): Promise<Outcome> {
  const deadline = Date.now() + minutes * 60_000;
  // After another Mind takes over the connection, Self stays at level 0 until the next level up.
  const levelOf = () => Math.max(agent.self.level, knownLevel);
  const avoid = new Map<number, number>();
  let spot = 0;

  while (levelOf() < goal) {
    if (Date.now() > deadline) return failed("outOfTime");
    if (agent.self.dead) {
      const alive = await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
      log.push(alive ? "died, released" : "died, still dead");
      if (!alive) return failed("stillDead");
      continue;
    }

    await trainBlades(agent);

    const me = agent.self.objectId;
    const attacker = me === undefined ? undefined : agent.surroundings.attacking(me)[0];
    if (attacker) {
      const r = await kill(agent, attacker.objectId);
      log.push(`defended vs ${attacker.name} L${attacker.level}: ${r.ok ? "ok" : r.reason}`);
      continue;
    }

    if (agent.self.healthPercent < 90) {
      await rest(agent, 100);
      continue;
    }

    const level = levelOf();
    const pos = agent.self.position;
    if (!pos) return failed("noPosition");
    // Yellow Mobs kill a Guardian with no armour: fight blue ones by default.
    const low = level + band[0];
    const high = level + band[1];
    const mob = agent.surroundings.npcs
      .filter((n) => n.realm === "None" && !n.peaceful && n.healthPercent > 0 && n.level >= low && n.level <= high && (avoid.get(n.objectId) ?? 0) < 2)
      .map((n) => ({ n, d: Math.hypot(n.position.x - pos.x, n.position.y - pos.y) }))
      .sort((a, b) => a.d - b.d)[0]?.n;
    if (!mob) {
      // Nothing to fight here: roam to the next spot, if there are any.
      if (spots.length === 0) return failed("noMob");
      const to = spots[spot++ % spots.length];
      const ran = await runTo(agent, to, 200);
      log.push(`roamed to X:${to.x} Y:${to.y}: ${ran.ok ? "ok" : ran.reason}`);
      continue;
    }

    const r = await kill(agent, mob.objectId);
    log.push(`${mob.name} L${mob.level}: ${r.ok ? "ok" : r.reason} (lvl ${levelOf()}, hp ${agent.self.healthPercent}%)`);
    if (!r.ok) avoid.set(mob.objectId, (avoid.get(mob.objectId) ?? 0) + 1);
  }

  await trainBlades(agent);
  return succeeded;
}
