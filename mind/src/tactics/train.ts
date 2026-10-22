import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** In milliseconds. The server answers a `/train` with the new skills at once. */
export const TRAIN_TIMEOUT_MS = 10_000;

/**
 * Trains the specialization `line` up to `level` with `/train`, as a Human types it, and succeeds once the skills Percept shows it there.
 * It fails with `notTrained` if they don't, for example without enough spec points.
 */
export async function train(agent: Agent, line: string, level: number): Promise<Outcome> {
  const trained = () => (agent.self.specs.get(line) ?? 0) >= level;

  if (trained())
    return succeeded;

  const done = await agent.sendAndWait({ name: "command", text: `/train ${line} ${level}` }, trained, TRAIN_TIMEOUT_MS);
  return done ? succeeded : failed("notTrained");
}

/** The highest level `line` can reach from `level` with `points`: each next level costs that level in points. */
export function affordableLevel(level: number, points: number): number {
  while (points >= level + 1) {
    level++;
    points -= level;
  }

  return level;
}
