import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { train } from "../../../mind/src/tactics/train.ts";
import { describe, fight } from "./fight.ts";

/**
 * Spends spec points on Blades, which can't go above my level, then the rest on Parry (more parries open Return Blade)
 * and Celtic Dual (the left hand hits more and harder), lower first. The next level always brings enough for Blades.
 */
export async function trainBlades(agent: Agent): Promise<void> {
  const level = agent.self.level;
  if (level === 0) return;
  const blades = agent.self.specs.get("Blades") ?? 1;
  let target = blades;
  let points = agent.self.specPoints;
  while (target < level && points >= target + 1) points -= ++target;
  if (target > blades) await train(agent, "Blades", target);

  const lines = ["Parry", "Celtic Dual"];
  const from = new Map(lines.map((l) => [l, agent.self.specs.get(l) ?? 1]));
  const to = new Map(from);
  for (;;) {
    const next = lines.filter((l) => to.get(l)! < level && points >= to.get(l)! + 1).sort((a, b) => to.get(a)! - to.get(b)!)[0];
    if (!next) break;
    to.set(next, to.get(next)! + 1);
    points -= to.get(next)!;
  }
  // `train` sends the line unquoted, which the server ignores for "Celtic Dual".
  for (const l of lines)
    if (to.get(l)! > from.get(l)!)
      await agent.sendAndWait({ name: "command", text: `/train "${l}" ${to.get(l)}` }, () => (agent.self.specs.get(l) ?? 0) >= to.get(l)!, 10_000);
}

export interface GrindOptions {
  /** Stop at this level. */
  goal: number;
  log?: string[];
  /** After another Mind takes over the connection, Self stays at level 0 until the next level up. */
  knownLevel?: number;
  /** Where to roam when nothing is in view. */
  spots?: { x: number; y: number; z: number }[];
  minutes?: number;
  /** Mob levels to fight, relative to mine. */
  band?: [number, number];
  /** Rest to full when health drops below this percent. */
  restBelow?: number;
  /** Stop once money reaches this many copper. */
  money?: number;
  /** A Mob name to seek out (a Kill Task's), even 3000 units further; grind stops with `preferredKilled` once one dies. */
  prefer?: string;
  /** Mob names never to pull, such as blackthorns, which call lunantishee to their defence. */
  exclude?: string[];
  /** Rest up to this percent. */
  restTo?: number;
  /** Seconds spent fighting, resting, roaming and dead, added up here. */
  times?: Record<string, number>;
}

/** Kills Mobs in a level band, nearest first but preferring higher levels, until `goal` level, resting between fights. */
export async function grind(agent: Agent, { goal, log = [], knownLevel = 0, spots = [], minutes = 25, band = [-1, -1], restBelow = 90, money = Infinity, prefer, exclude = [], restTo = 100, times = {} }: GrindOptions): Promise<Outcome> {
  const deadline = Date.now() + minutes * 60_000;
  const levelOf = () => Math.max(agent.self.level, knownLevel);
  const avoid = new Map<number, number>();
  let spot = 0;
  let roams = 0;
  const timed = async <T>(what: string, p: Promise<T>): Promise<T> => {
    const t = Date.now();
    try { return await p; } finally { times[what] = Math.round((times[what] ?? 0) + (Date.now() - t) / 1000); }
  };

  while (levelOf() < goal) {
    if (Date.now() > deadline) return failed("outOfTime");
    if (agent.inventory.money >= money) return failed("enoughMoney");
    if (agent.self.dead) {
      const alive = await timed("dead", agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000));
      log.push(alive ? "died, released" : "died, still dead");
      if (!alive) return failed("stillDead");
      continue;
    }

    await trainBlades(agent);

    const me = agent.self.objectId;
    const attacker = me === undefined ? undefined : agent.surroundings.attacking(me)[0];
    if (attacker) {
      const r = await timed("fight", kill(agent, attacker.objectId));
      log.push(`defended vs ${attacker.name} L${attacker.level}: ${r.ok ? "ok" : r.reason}`);
      continue;
    }

    if (agent.self.healthPercent < restBelow) {
      await timed("rest", rest(agent, restTo));
      continue;
    }

    const level = levelOf();
    const pos = agent.self.position;
    if (!pos) return failed("noPosition");
    // Yellow Mobs kill a Guardian with no armour: fight blue ones by default.
    const low = level + band[0];
    const high = level + band[1];
    const mob = agent.surroundings.npcs
      .filter((n) => n.realm === "None" && !n.peaceful && n.healthPercent > 0 && !exclude.includes(n.name) && (n.level >= low || n.name === prefer) && (n.level <= high || n.name === prefer) && (avoid.get(n.objectId) ?? 0) < 2)
      // A Mob a level higher pays about double: worth running 1500 units further for.
      .map((n) => ({ n, d: Math.hypot(n.position.x - pos.x, n.position.y - pos.y) - (n.level - low) * 1500 - (n.name === prefer ? 3000 : 0) }))
      .sort((a, b) => a.d - b.d)[0]?.n;
    if (!mob) {
      // Nothing to fight here: roam to the next spot, if there are any.
      if (spots.length === 0) return failed("noMob");
      // Twice round every spot with nothing in the band: the area is camped out, so go elsewhere.
      if (++roams > spots.length * 2) return failed("areaEmpty");
      const to = spots[spot++ % spots.length];
      const ran = await timed("roam", runTo(agent, to, 200));
      log.push(`roamed to X:${to.x} Y:${to.y}: ${ran.ok ? "ok" : ran.reason}`);
      continue;
    }

    roams = 0;
    // `kill` gives up after 30 s without damage, which a long run to the Mob already takes: run most of the way first.
    if (Math.hypot(mob.position.x - pos.x, mob.position.y - pos.y) > 2000) {
      const ran = await timed("roam", runTo(agent, mob.position, 800));
      if (!ran.ok) avoid.set(mob.objectId, (avoid.get(mob.objectId) ?? 0) + 1);
      continue;
    }
    const xpBefore = agent.self.level * 1000 + agent.self.levelPermill;
    const stats = await timed("fight", fight(agent, mob.objectId));
    const r = stats.outcome;
    const gained = agent.self.level * 1000 + agent.self.levelPermill - xpBefore;
    log.push(`${mob.name} L${mob.level} at X:${Math.round(mob.position.x)} Y:${Math.round(mob.position.y)}: ${describe(stats)} +${gained}‰ (lvl ${levelOf()}, hp ${agent.self.healthPercent}%, end ${agent.self.endurancePercent}%)`);
    if (!r.ok) avoid.set(mob.objectId, (avoid.get(mob.objectId) ?? 0) + 1);
    if (r.ok && mob.name === prefer) return failed("preferredKilled");
  }

  await trainBlades(agent);
  return succeeded;
}
