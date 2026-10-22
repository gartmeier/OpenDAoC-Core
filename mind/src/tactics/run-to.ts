import type { Percept } from "../protocol.ts";
import type { Position } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** In milliseconds. Longer than any run inside a region takes. */
export const RUN_TIMEOUT_MS = 300_000;

/**
 * Runs to `target`, `within` units short of the end of the route if given, and succeeds on `arrived` (ADR 0007).
 * It fails with the reason the run stopped, in camel case like every Tactic's reasons (`unreachable`, `movedByServer`), or with `timeout`.
 */
export async function runTo(agent: Agent, target: Position, within?: number): Promise<Outcome> {
  const ended = await agent.sendAndWait(
    { name: "moveTo", x: target.x, y: target.y, z: target.z, ...(within === undefined ? {} : { within }) },
    runEnd,
    RUN_TIMEOUT_MS,
  );
  return ended ?? failed("timeout");
}

/**
 * How a run ended, from its `arrived` or `stopped`, or nothing for any other Percept.
 * A run replaced by a new `moveTo` ends `stopped` with `Replaced` just before the new one starts, so that isn't the new run's end.
 */
export function runEnd(percept: Percept | undefined): Outcome | undefined {
  if (percept?.kind === "arrived")
    return succeeded;

  if (percept?.kind === "stopped" && percept.reason !== "Replaced")
    return failed(percept.reason[0]!.toLowerCase() + percept.reason.slice(1));

  return undefined;
}
