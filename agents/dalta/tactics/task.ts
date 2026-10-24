import { type Agent, failed, type Outcome } from "../../../mind/src/tactic.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { talkTo } from "../../../mind/src/tactics/trade.ts";

/** Sentinel Eimile in Ardagh, a named guard who gives Kill Tasks. */
export const EIMILE = { name: "Sentinel Eimile", x: 352314, y: 553250, z: 5100 };

/**
 * Goes to the task guard, hands in a finished Kill Task (talking to the guard does it) and asks for a new one.
 * Succeeds with the Mob to kill in `log`; fails with `noGuard`, or `noTask` if the guard gave none (e.g. the limit per level).
 */
export async function takeTask(agent: Agent, log: string[] = []): Promise<Outcome & { mob?: string }> {
  await runTo(agent, EIMILE, 100);
  const guard = agent.surroundings.npcs.find((n) => n.name === EIMILE.name);
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
