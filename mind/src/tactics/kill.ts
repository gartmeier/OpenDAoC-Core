import type { Percept } from "../protocol.ts";
import { distance, headingTowards, type Position } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** How close, in game units, a player must stand to hit in melee (`GamePlayer.MeleeAttackRange` in the server). Static knowledge, as a Human learns it. */
export const MELEE_RANGE = 128;

/** How far short of the Mob a run to it ends, so that it stands well inside melee range. */
export const CLOSE_IN = 80;

/** In milliseconds: a fight in which neither side's health falls for this long has stalled, for example because the server ignored the attack. */
export const NO_DAMAGE_MS = 30_000;

/** How far, in heading units of 4096 to a full turn, the Agent may face away from the Mob before it turns to it. The server lets a player hit what's within 60° either side of where it faces. */
const FACING_SLACK = 300;

/** What one Percept means for a fight. */
type FightNews = "mobDied" | "died" | "gone" | "unreachable" | "runEnded" | "news";

/**
 * Kills the Mob: targets it, attacks, runs to it and faces it, and follows it when it moves out of melee range, until it dies.
 * It fails with `died` (the Agent died), `outOfView` (the Mob went out of view or left the world alive), `unreachable` (no route gets to it) or `noDamage` (neither side's health fell for `NO_DAMAGE_MS`).
 * Attack is off again however it ends, unless the Agent was stopped, which leaves that to whoever stopped it.
 */
export async function kill(agent: Agent, mobId: number): Promise<Outcome> {
  const { self, surroundings } = agent;

  if (!surroundings.npc(mobId))
    return failed("outOfView");

  await agent.send({ name: "target", objectId: mobId });
  await agent.send({ name: "attack", on: true });

  try {
    let lastDamage = agent.clock.now();
    // Where the run in progress is going, and the heading the Agent last turned to since it last stood still.
    let runningTo: Position | undefined;
    let facing: number | undefined;
    let mobHealth = surroundings.npc(mobId)?.healthPercent ?? 100;
    let ownHealth = self.healthPercent;

    while (true) {
      const mob = surroundings.npc(mobId);
      const at = self.position;

      if (!mob || !at)
        return failed("outOfView");

      if (distance(at, mob.position) > MELEE_RANGE) {
        // Follows the Mob as news of where it is arrives, never more often.
        if (!runningTo || distance(runningTo, mob.position) > CLOSE_IN) {
          runningTo = mob.position;
          await agent.send({ name: "moveTo", x: mob.position.x, y: mob.position.y, z: mob.position.z, within: CLOSE_IN });
        }
      } else if (!runningTo) {
        const heading = headingTowards(at, mob.position);

        if (heading !== undefined && turn(facing ?? self.heading, heading) > FACING_SLACK) {
          facing = heading;
          await agent.send({ name: "face", heading });
        }
      }

      const remaining = NO_DAMAGE_MS - (agent.clock.now() - lastDamage);
      const news = remaining > 0 ? await agent.waitFor((percept) => fightNews(percept, mobId, self.objectId), remaining) : undefined;

      switch (news) {
        case undefined:
          return failed("noDamage");
        case "mobDied":
          return succeeded;
        case "died":
          return failed("died");
        case "gone":
          return failed("outOfView");
        case "unreachable":
          return failed("unreachable");
        case "runEnded":
          runningTo = undefined;
          facing = undefined;
          break;
      }

      // Only health falling on either side is damage: the server sends misses and refused attacks as combat text too.
      const mobNow = surroundings.npc(mobId)?.healthPercent ?? mobHealth;

      if (mobNow < mobHealth || self.healthPercent < ownHealth)
        lastDamage = agent.clock.now();

      mobHealth = mobNow;
      ownHealth = self.healthPercent;
    }
  } finally {
    if (!agent.stopped)
      await agent.send({ name: "attack", on: false });
  }
}

function fightNews(percept: Percept | undefined, mobId: number, ownId: number | undefined): FightNews | undefined {
  switch (percept?.kind) {
    case "died":
      return percept.objectId === ownId ? "died" : undefined;
    case "objectUpdate":
      if (percept.objectId !== mobId)
        return undefined;

      return percept.healthPercent === 0 ? "mobDied" : "news";
    // A dead NPC leaves the world as object type 0.
    case "objectRemove":
      if (percept.objectId !== mobId)
        return undefined;

      return percept.objectType === 0 ? "mobDied" : "gone";
    case "objectDelete":
      return percept.objectId === mobId ? "gone" : undefined;
    case "arrived":
      return "runEnded";
    case "stopped":
      if (percept.reason === "Replaced")
        return undefined;

      return percept.reason === "Unreachable" ? "unreachable" : "runEnded";
    case "status":
      return "news";
    default:
      return undefined;
  }
}

/** How far apart two headings are, either way round. */
function turn(from: number, to: number): number {
  const difference = Math.abs(from - to) % 4096;
  return Math.min(difference, 4096 - difference);
}
