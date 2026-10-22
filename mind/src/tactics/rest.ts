import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** In milliseconds. Longer than a level 5 Character takes to sit back to full health. */
export const REST_TIMEOUT_MS = 300_000;

/**
 * Sits until health reaches `untilPercent`, then stands.
 * It fails with `attacked` as soon as an NPC attacks the Agent or its health drops, standing up first, or with `timeout`.
 */
export async function rest(agent: Agent, untilPercent: number): Promise<Outcome> {
  const { self, surroundings } = agent;
  const attacked = () => self.objectId !== undefined && surroundings.attacking(self.objectId).length > 0;

  if (attacked())
    return failed("attacked");

  let health = self.healthPercent;
  const ended = await agent.sendAndWait(
    { name: "sit" },
    () => {
      if (attacked() || self.healthPercent < health)
        return "attacked";

      health = self.healthPercent;
      return self.healthPercent >= untilPercent ? "rested" : undefined;
    },
    REST_TIMEOUT_MS,
  );
  await agent.send({ name: "stand" });
  return ended === "rested" ? succeeded : failed(ended ?? "timeout");
}
