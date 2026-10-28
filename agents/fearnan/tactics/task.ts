import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { talkTo } from "../../../mind/src/tactics/trade.ts";
import { whisper } from "../../../mind/src/tactics/whisper.ts";
import { groupFight, hostile, upkeep } from "./group.ts";

type Point = { x: number; y: number; z: number };
const away = (agent: Agent, p: { x: number; y: number }) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

/** Hands in a finished Kill Task at the guard and asks for the next. */
export async function turnIn(agent: Agent, guard: Point, guardName: string): Promise<Outcome & { mob?: string }> {
  if (away(agent, guard) > 300) await runTo(agent, { ...guard, y: guard.y + 100 }, 200);
  const npc = agent.surroundings.npcs.find((n) => n.name === guardName);
  if (!npc) return failed("noGuard");
  await talkTo(agent, npc.objectId);
  await agent.waitFor((p) => p?.kind === "message" && /finish the Kill Task|reward/.test(p.text) || undefined, 3_000);
  // Listen before whispering: `whisper` waits for the guard's answer itself, so a wait started after it misses the task.
  const listening = agent.waitFor((p) => p?.kind === "message" && /and kill an? ([^.]+?) and return/.exec(p.text)?.[1], 6_000);
  await whisper(agent, npc.objectId, "task");
  const given = await listening;
  return given ? { ...succeeded, mob: given } : failed("noTask");
}

/**
 * Runs Kill Tasks with the Group member `name` for `rounds`: goes to `area`, has the Pet kill the nearest `mob` there (a shared kill
 * counts for both of us), heals and fights what attacks the group on the way, and turns in at the guard.
 */
export async function taskLoop(agent: Agent, name: string, guard: Point, guardName: string, mob: string, area: Point, rounds: number, stopAtLevel = 20, maxAbove = 0): Promise<Outcome & { done?: number; mob?: string }> {
  let done = 0, idle = 0, killed = false;
  while (done < rounds && agent.self.level < stopAtLevel) {
    // A timer, not a microtask: a loop that awaits nothing else must still let the connection breathe.
    await agent.waitFor(() => undefined, 50);
    if (agent.self.dead) return { ...failed("died"), done };

    const foe = hostile(agent)[0];
    if (foe) {
      const o = await groupFight(agent, foe.objectId, name);
      if (o.ok && foe.name === mob) killed = true;
      continue;
    }

    if (killed) {
      const t = await turnIn(agent, guard, guardName);
      if (!t.ok) return { ...t, done };
      killed = false;
      done++;
      if (t.mob !== mob) return { ...failed("newMob"), done, mob: t.mob };
      continue;
    }

    const ready = await upkeep(agent, name);
    if (ready === "rested") continue;
    if (!ready.ok) return { ...ready, done };
    // Full Power for both before a pull: a siabra fight drains it, and a second one then finds us empty.
    const matePower = () => agent.group.named(name)?.powerPercent ?? 100;
    if (agent.self.powerPercent < 90 || matePower() < 85) {
      await Promise.race([
        rest(agent, 100),
        agent.waitFor(() => (agent.self.powerPercent >= 95 && matePower() >= 90) || hostile(agent)[0] ? true : undefined, 120_000),
      ]);
      await agent.send({ name: "stand" });
      continue;
    }

    // A kill counts for the mate only when it's close: wait up to 90 s for it to catch up before pulling.
    const mateAway = () => { const p = agent.group.named(name)?.position; return p ? away(agent, p) : 0; };
    if (mateAway() > 1500 && !agent.group.named(name)?.dead) {
      await agent.waitFor(() => mateAway() <= 1500 || hostile(agent)[0] ? true : undefined, 90_000);
      if (hostile(agent)[0]) continue;
    }

    // A lone one, not above its level, the lowest first: ghastly siabras come two at a time and killed Orla when they did.
    const crowded = (n: { objectId: number; position: Point }) => agent.surroundings.npcs.some((o) => o.objectId !== n.objectId
      && o.realm === "None" && !o.peaceful && !o.pet && o.healthPercent > 0 && o.level >= agent.self.level - 3
      && Math.hypot(o.position.x - n.position.x, o.position.y - n.position.y) < 900);
    const target = agent.surroundings.npcs
      .filter((n) => n.name === mob && n.healthPercent > 0 && !n.peaceful && n.level <= agent.self.level + maxAbove && !crowded(n))
      .sort((a, b) => a.level - b.level || away(agent, a.position) - away(agent, b.position))[0];
    if (target && away(agent, target.position) < 3500) {
      idle = 0;
      if (away(agent, target.position) > 1500) {
        const at = agent.self.position!;
        const d = away(agent, target.position);
        await runTo(agent, { x: Math.round(target.position.x - (target.position.x - at.x) / d * 1000), y: Math.round(target.position.y - (target.position.y - at.y) / d * 1000), z: target.position.z }, 150);
        continue;
      }
      const o = await groupFight(agent, target.objectId, name);
      if (o.ok) killed = true;
      continue;
    }

    // In legs of at most 2000, so the mate check above sees a mate that doesn't follow.
    if (away(agent, area) > 600) {
      const at = agent.self.position!, d = away(agent, area);
      await runTo(agent, d > 2300 ? { x: Math.round(at.x + (area.x - at.x) / d * 2000), y: Math.round(at.y + (area.y - at.y) / d * 2000), z: area.z } : area, 300);
      continue;
    }
    if (++idle > 6) return { ...failed("noMob"), done };
    await agent.waitFor(() => undefined, 10_000);
  }
  return { ...succeeded, done };
}
