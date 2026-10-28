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
