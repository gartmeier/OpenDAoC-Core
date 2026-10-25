import { type Agent, failed, type Outcome } from "../../../mind/src/tactic.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { talkTo } from "../../../mind/src/tactics/trade.ts";
import { grind, type GrindOptions } from "./grind.ts";

/** Sentinel Eimile in Ardagh, a named guard who gives Kill Tasks. */
export const EIMILE = { name: "Sentinel Eimile", x: 352314, y: 553250, z: 5100 };
/** Sentinel Ornora in Tir na mBeo (Lough Derg), by the Healer Ionhar. */
export const ORNORA = { name: "Sentinel Ornora", x: 345307, y: 526666, z: 5495 };

/**
 * Goes to the task guard, hands in a finished Kill Task (talking to the guard does it) and asks for a new one.
 * Succeeds with the Mob to kill in `log`; fails with `noGuard`, or `noTask` if the guard gave none (e.g. the limit per level).
 */
export async function takeTask(agent: Agent, log: string[] = [], at = EIMILE): Promise<Outcome & { mob?: string }> {
  await runTo(agent, at, 100);
  const guard = agent.surroundings.npcs.find((n) => n.name === at.name);
  if (!guard) return failed("noGuard");

  const xp = agent.self.level * 1000 + agent.self.levelPermill;
  const money = agent.inventory.money;
  await talkTo(agent, guard.objectId);
  await agent.waitFor(() => false, 1500);
  log.push(`handed in: +${agent.self.level * 1000 + agent.self.levelPermill - xp}‰ +${agent.inventory.money - money}c`);

  await agent.send({ name: "target", objectId: guard.objectId });
  const said = await agent.sendAndWait({ name: "command", text: "/whisper task" }, (p) =>
    p?.kind === "message" && /kill an? (.+?) and return/.exec(p.text)?.[1], 5_000);
  if (typeof said !== "string") return failed("noTask");
  log.push(`task: kill ${said}`);
  return { ok: true, mob: said };
}

/**
 * Takes a Kill Task at `at`, grinds (with `opts`) until the task Mob dies, runs back and hands it in, over and over.
 * Grinding also kills what's in the band on the way. `current` is the Mob of a task already taken.
 * When the guard gives no more tasks (the limit per level), grinds `camp` (other options, such as spots and band) until the next level, then goes back for tasks.
 */
export async function taskLoop(agent: Agent, minutes: number, at: typeof EIMILE, opts: Omit<GrindOptions, "minutes" | "prefer" | "goal">, current?: string, camp?: Partial<GrindOptions>): Promise<Outcome> {
  const deadline = Date.now() + minutes * 60_000;
  const log = opts.log ?? [];
  const times = opts.times ?? {};
  const left = () => (deadline - Date.now()) / 60_000;
  while (left() > 0) {
    if (!current) {
      const t = Date.now();
      const task = await takeTask(agent, log, at);
      times.town = Math.round((times.town ?? 0) + (Date.now() - t) / 1000);
      if (!task.ok || !task.mob) {
        if (!camp) return task;
        const r = await grind(agent, { ...opts, ...camp, log, times, goal: agent.self.level + 1, minutes: left() });
        if (!r.ok) return r;
        continue;
      }
      current = task.mob;
    }
    const r = await grind(agent, { ...opts, log, times, goal: 51, prefer: current, minutes: left() });
    if (r.ok || r.reason !== "preferredKilled") return r;
    current = undefined;
  }
  return failed("outOfTime");
}
