import { headingTowards } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";
import { runTo } from "./run-to.ts";

/** How far short of the speaker the Agent stops, along its route, in game units, so that it stands next to them rather than inside them. */
export const STAND_OFF = 100;

/**
 * Runs to where `speaker` is and turns to face them. The Harness routes there, ends the run the stand-off distance short of them along the route, and arrives at once if it's that close already (ADR 0007).
 * It fails with `unknownSpeaker` for a speaker the Agent doesn't know of, who is somewhere it couldn't know, or with the reason the run stopped.
 */
export async function come(agent: Agent, speaker: string): Promise<Outcome> {
  const { self, surroundings } = agent;
  const to = surroundings.named(speaker)?.position;

  if (!to)
    return failed("unknownSpeaker");

  const ran = await runTo(agent, to, STAND_OFF);

  if (!ran.ok)
    return ran;

  // Where the speaker is now, which may not be where they said `come`.
  const from = self.position;
  const now = surroundings.named(speaker)?.position;
  const heading = from && now ? headingTowards(from, now) : undefined;

  if (heading !== undefined)
    await agent.send({ name: "face", heading });

  return succeeded;
}
