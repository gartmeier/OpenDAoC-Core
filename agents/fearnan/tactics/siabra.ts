import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { groupGrind } from "./group.ts";
import { turnIn } from "./task.ts";

type Point = { x: number; y: number; z: number };

/**
 * Kill Tasks for a Mob that's only now and then fightable (ghastly siabras turn peaceful for a while): grinds with the mate at
 * `camp` on whatever fits `range` (the task Mob too, lone ones first) until the task Mob dies, then hands it in at the guard,
 * says so in group chat, and goes back. `rounds` tasks, or until `minutes` run out.
 */
export async function taskGrind(agent: Agent, name: string, guard: Point, guardName: string, mob: string, camp: Point, rounds: number, minutes: number, range = [-2, 0], avoid: string[] = [], stopAtLevel = 20): Promise<Outcome & { done?: number; kills?: number }> {
  const end = agent.clock.now() + minutes * 60_000;
  let done = 0, kills = 0;
  while (done < rounds && agent.clock.now() < end && agent.self.level < stopAtLevel) {
    let finished = false;
    // Listen for the task's end in chat while grinding.
    void agent.waitFor((p) => { if (p?.kind === "message" && /return to .+ to receive your reward/.test(p.text)) finished = true; return finished || undefined; }, minutes * 60_000);
    const g = await groupGrind(agent, name, (end - agent.clock.now()) / 60_000, stopAtLevel, range, avoid, true, camp, () => finished);
    kills += g.kills ?? 0;
    if (!g.ok) return { ...g, done, kills };
    if (!finished) break;
    await agent.send({ name: "command", text: "/g Task Mob down. Off to the guard to hand in, then back here." });
    const t = await turnIn(agent, guard, guardName);
    if (!t.ok) return { ...t, done, kills };
    done++;
    if (t.mob !== mob) return { ...failed("newMob"), done, kills };
  }
  return { ...succeeded, done, kills };
}

/**
 * Siabra Kill Tasks with the mate: from `camp` (the siabra west edge), rests both to full Power, has the Pet pull the awake siabra
 * with the fewest others around it from 1400 away (the mate mezzes an add), fights whatever comes, and hands in at the guard once the
 * task says so. While the siabras are peaceful it grinds the maidens at the camp. `rounds` tasks, or until `minutes` run out.
 */
export async function siabraRun(agent: Agent, name: string, guard: Point, guardName: string, camp: Point, rounds: number, minutes: number, stopAtLevel = 20): Promise<Outcome & { done?: number; kills?: number }> {
  const { groupFight, hostile, upkeep } = await import("./group.ts");
  const { rest } = await import("../../../mind/src/tactics/rest.ts");
  const { runTo } = await import("../../../mind/src/tactics/run-to.ts");
  const end = agent.clock.now() + minutes * 60_000;
  const away = (p: { x: number; y: number }) => { const at = agent.self.position; return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity; };
  const awake = () => agent.surroundings.npcs.filter((n) => n.name === "ghastly siabra" && !n.peaceful && n.healthPercent > 0
    && n.level <= agent.self.level + 1 && Math.hypot(n.position.x - camp.x, n.position.y - camp.y) < 4500);
  let done = 0, kills = 0, finished = false;
  const listen = () => void agent.waitFor((p) => { if (p?.kind === "message" && /return to .+ to receive your reward/.test(p.text)) finished = true; return finished || undefined; }, minutes * 60_000);
  listen();
  while (done < rounds && agent.clock.now() < end && agent.self.level < stopAtLevel) {
    await agent.waitFor(() => undefined, 50);
    if (agent.self.dead) return { ...failed("died"), done, kills };
    const foe = hostile(agent)[0];
    if (foe) { if ((await groupFight(agent, foe.objectId, name)).ok) kills++; continue; }

    if (finished) {
      await agent.send({ name: "command", text: "/g Task siabra down. To Ornora to hand in, then back to the west edge." });
      const t = await turnIn(agent, guard, guardName);
      if (!t.ok) return { ...t, done, kills };
      done++; finished = false; listen();
      if (t.mob !== "ghastly siabra") return { ...failed("newMob"), done, kills };
      continue;
    }

    // Back to camp in legs of 2000, waiting for the mate to keep up.
    const mateAt = () => agent.group.named(name)?.position;
    if (away(camp) > 1500) {
      const m = mateAt();
      if (m && away(m) > 1800 && !agent.group.named(name)?.dead) { await agent.waitFor(() => { const p = mateAt(); return !p || away(p) < 1200 || hostile(agent)[0] ? true : undefined; }, 60_000); }
      const at = agent.self.position!, d = away(camp);
      await runTo(agent, d > 2300 ? { x: Math.round(at.x + (camp.x - at.x) / d * 2000), y: Math.round(at.y + (camp.y - at.y) / d * 2000), z: camp.z } : camp, 300);
      continue;
    }

    const ready = await upkeep(agent, name);
    if (ready === "rested") continue;
    if (!ready.ok) return { ...ready, done, kills };

    const all = awake();
    if (!all.length) {
      // Peaceful: maidens until one wakes.
      const g = await groupGrind(agent, name, Math.min(10, (end - agent.clock.now()) / 60_000), stopAtLevel, [-2, 0], ["ghastly siabra"], true, camp, () => awake().length > 0);
      kills += g.kills ?? 0;
      if (!g.ok && g.reason !== "noMob") return { ...g, done, kills };
      continue;
    }
    const matePower = () => agent.group.named(name)?.powerPercent ?? 100;
    if (agent.self.powerPercent < 90 || matePower() < 85) {
      await Promise.race([rest(agent, 100), agent.waitFor(() => (agent.self.powerPercent >= 95 && matePower() >= 90) || hostile(agent)[0] ? true : undefined, 120_000)]);
      await agent.send({ name: "stand" });
      continue;
    }
    const m = mateAt();
    // Go to the mate rather than wait: she keeps a leash of ~1500 and stood still while I waited for her within 1000 (20 min stalled).
    if (m && away(m) > 1600) { await runTo(agent, { x: m.x, y: m.y, z: agent.self.position!.z }, 300); continue; }

    const crowd = (n: { objectId: number; position: Point }) => all.filter((o) => o.objectId !== n.objectId && Math.hypot(o.position.x - n.position.x, o.position.y - n.position.y) < 700).length;
    const target = all.map((n) => ({ n, c: crowd(n), d: away(n.position) })).sort((a, b) => a.c - b.c || a.d - b.d)[0].n;
    const d = away(target.position);
    if (d > 1500) {
      const at = agent.self.position!;
      await runTo(agent, { x: Math.round(target.position.x - (target.position.x - at.x) / d * 1400), y: Math.round(target.position.y - (target.position.y - at.y) / d * 1400), z: at.z }, 100);
      continue;
    }
    if ((await groupFight(agent, target.objectId, name)).ok) kills++;
  }
  return { ...succeeded, done, kills };
}
