import { saidBy } from "../say-line.ts";
import { distance } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";
import { runTo } from "./run-to.ts";

/** How close, in game units, a player must stand to whisper to an NPC (`WorldMgr.WHISPER_DISTANCE`). Static knowledge, as a Human learns it. */
export const WHISPER_RANGE = 512;

/** How far short of the NPC a run to it ends, well inside whispering range. */
export const WHISPER_CLOSE_IN = 400;

/** In milliseconds. An NPC answers a whisper at once. */
export const WHISPER_TIMEOUT_MS = 5_000;

/**
 * Gets within whispering range of the NPC, targets it and whispers `keyword`, as clicking a [keyword] in its words does in the client, and succeeds once the NPC answers in words.
 * It fails with `outOfView` (the NPC isn't in view), `tooFar` (the Agent can't get within reach) or `timeout` (no answer came).
 */
export async function whisper(agent: Agent, npcId: number, keyword: string): Promise<Outcome> {
  const { self, surroundings } = agent;
  const npc = surroundings.npc(npcId);

  if (!npc)
    return failed("outOfView");

  const near = () => self.position !== undefined && distance(self.position, npc.position) <= WHISPER_RANGE;

  if (!near()) {
    const ran = await runTo(agent, npc.position, WHISPER_CLOSE_IN);

    if (!ran.ok || !near())
      return failed("tooFar");
  }

  await agent.send({ name: "target", objectId: npcId });
  const answer = await agent.sendAndWait({ name: "command", text: `/whisper ${keyword}` }, (percept) => saidBy(npc.name, percept), WHISPER_TIMEOUT_MS);
  return answer ? succeeded : failed("timeout");
}
