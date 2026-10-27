import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
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
  await whisper(agent, npc.objectId, "task");
  const given = await agent.waitFor((p) => p?.kind === "message" && /and kill an? ([^.]+?) and return/.exec(p.text)?.[1], 3_000);
  return given ? { ...succeeded, mob: given } : failed("noTask");
}

/**
 * Runs Kill Tasks with the Group member `name` for `rounds`: goes to `area`, has the Pet kill the nearest `mob` there (a shared kill
 * counts for both of us), heals and fights what attacks the group on the way, and turns in at the guard.
 */
export async function taskLoop(agent: Agent, name: string, guard: Point, guardName: string, mob: string, area: Point, rounds: number, stopAtLevel = 20): Promise<Outcome & { done?: number; mob?: string }> {
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

    const target = agent.surroundings.npcs
      .filter((n) => n.name === mob && n.healthPercent > 0)
      .sort((a, b) => away(agent, a.position) - away(agent, b.position))[0];
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

    if (away(agent, area) > 600) { await runTo(agent, area, 300); continue; }
    if (++idle > 6) return { ...failed("noMob"), done };
    await agent.waitFor(() => undefined, 10_000);
  }
  return { ...succeeded, done };
}
