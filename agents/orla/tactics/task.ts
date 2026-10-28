import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { talkTo } from "../../../mind/src/tactics/trade.ts";
import { whisper } from "../../../mind/src/tactics/whisper.ts";

type Point = { x: number; y: number; z: number };

export const GRANIA = { name: "Sentinel Grania", at: { x: 314041, y: 636846, z: 4849 } };
export const ORNORA = { name: "Sentinel Ornora", at: { x: 345307, y: 526766, z: 5495 } };

/**
 * Runs to the guard, hands in a finished Kill Task (if one is done) and asks for the next.
 * Ends with the Mob and direction the guard named, or `noTask` (no more tasks this level, or the guard said nothing).
 */
export async function turnIn(agent: Agent, guard: { name: string; at: Point }): Promise<Outcome & { task?: string; reward?: string }> {
  const ran = await runTo(agent, guard.at, 200);
  if (!ran.ok) return ran;
  const npc = agent.surroundings.npcs.find((n) => n.name === guard.name);
  if (!npc) return failed("noGuard");
  const before = agent.self.levelPermill;
  await talkTo(agent, npc.objectId);
  await agent.waitFor(() => undefined, 1500);
  const reward = agent.self.levelPermill !== before ? `${agent.self.levelPermill - before}‰` : undefined;
  // Listen before whispering: the guard answers before `whisper` returns.
  const answer = agent.waitFor((p) => (p?.kind === "message" && /Search to the ([^.]+?) and kill an? ([^.]+?) and return/.exec(p.text)?.slice(1).join(": ")) || undefined, 5000).catch(() => undefined);
  await whisper(agent, npc.objectId, "task");
  const task = await answer;
  return task ? { ...succeeded, task, reward } : { ...failed("noTask"), reward };
}

/**
 * Kill Tasks with a partner who pulls with a pet: assists with `duo` until the server says the task is done
 * ("You must now return to <guard>"), then turns in at the guard, takes the next task and does it again, `rounds` times.
 */
export async function taskDuo(agent: Agent, partner: string, guard: { name: string; at: Point }, rounds: number, minutesEach = 8): Promise<Outcome & { done: number; log: string[] }> {
  const { duo } = await import("./duo.ts");
  const log: string[] = [];
  let done = 0;
  for (let round = 0; round < rounds; round++) {
    let finished = false;
    const watch = agent.waitFor((p) => (p?.kind === "message" && p.text.includes(`return to ${guard.name}`)) || undefined, minutesEach * 60_000).then((x) => { finished = !!x; });
    const fought = await duo(agent, partner, (l) => [l - 1, l, l + 1], minutesEach, 99, [], false, 90, () => finished);
    void watch;
    if (!fought.ok) return { ...fought, done, log };
    if (!finished) { log.push("task not done in time"); break; }
    const t = await turnIn(agent, guard);
    log.push(`${t.reward ?? "?"} then ${t.ok ? t.task : t.reason}`);
    done++;
    if (!t.ok) break;
  }
  return { ...succeeded, done, log };
}
