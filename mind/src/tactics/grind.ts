import { distance, type KnownNpc, type Position } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";
import { kill } from "./kill.ts";
import { rest } from "./rest.ts";
import { RUN_TIMEOUT_MS, runEnd, runTo } from "./run-to.ts";
import { affordableLevel, train } from "./train.ts";

// The stand-in rules: deliberately simple, for LLM Planning to replace in milestone 5.

/** The level the grind ends at. */
export const GOAL_LEVEL = 5;

/** How far from home, in game units, the Agent looks for Mobs and wanders. */
export const LEASH = 1500;

/** Below this health percentage the Agent rests, up to `REST_UNTIL`. */
export const REST_BELOW = 50;
export const REST_UNTIL = 90;

/** The specialization every spec point goes into. A Guardian's training sword is a blade. */
export const TRAIN_LINE = "Blades";

/** In milliseconds. The server revives a released Character at its bind point at once. */
export const RELEASE_TIMEOUT_MS = 30_000;

/** Kill failures after which the Mob isn't tried again for the rest of the grind. */
const HOPELESS = new Set(["unreachable", "noDamage"]);

/**
 * Levels the Agent to `GOAL_LEVEL` around home, where it stands when the grind starts. Each step, in order:
 * if dead, it releases and runs home; it kills whatever attacks it; it trains the spec points it has; it rests when its health is low;
 * it kills the nearest Mob it can take on; and with none in view, it runs to a random spot near home.
 * At the goal it says so, logs a summary and succeeds. It fails with `homeUnreachable` when it can't get home after dying, `notReleased` when releasing doesn't bring it back to life, and `noPosition` when it doesn't know where it is to start with.
 */
export async function grind(agent: Agent): Promise<Outcome> {
  const { self, surroundings } = agent;
  const home = self.position;

  if (!home)
    return failed("noPosition");

  const tally = new Tally(agent.clock.now());
  // Mobs a kill gave up on for good, and the spec points a `/train` last failed with.
  const hopeless = new Set<number>();
  let untrainable: number | undefined;
  const target = () => nextMob(agent, home, hopeless);
  const attacker = () => (self.objectId === undefined ? undefined : nearest(agent, surroundings.attacking(self.objectId)));

  while (self.level < GOAL_LEVEL) {
    if (self.dead) {
      tally.deaths++;
      const back = await recover(agent, home);

      if (!back.ok)
        return back;

      continue;
    }

    const fight = attacker();

    if (fight) {
      tally.killed(await kill(agent, fight.objectId));
      continue;
    }

    const trained = self.specs.get(TRAIN_LINE) ?? 1;
    const affordable = affordableLevel(trained, self.specPoints);

    if (affordable > trained && self.specPoints !== untrainable) {
      const outcome = await train(agent, TRAIN_LINE, affordable);
      tally.note(outcome);

      if (!outcome.ok)
        untrainable = self.specPoints;

      continue;
    }

    if (self.healthPercent < REST_BELOW) {
      tally.rests++;
      tally.note(await rest(agent, REST_UNTIL));
      continue;
    }

    const mob = target();

    if (mob) {
      const outcome = await kill(agent, mob.objectId);
      tally.killed(outcome);

      if (!outcome.ok && HOPELESS.has(outcome.reason))
        hopeless.add(mob.objectId);

      continue;
    }

    await wander(agent, home, () => self.dead || attacker() !== undefined || target() !== undefined);
  }

  await agent.send({ name: "say", text: `I'm level ${GOAL_LEVEL} now.` });
  agent.log(tally.summary(agent.clock.now()));
  return succeeded;
}

/** The nearest NPC within the leash that the Agent can take on: hostile, near its level, alive, and fighting nobody else. */
export function nextMob(agent: Agent, home: Position, hopeless: ReadonlySet<number>): KnownNpc | undefined {
  const { self, surroundings } = agent;
  const mobs = surroundings.npcs.filter(
    (npc) =>
      npc.realm === "None" &&
      !npc.peaceful &&
      !npc.pet &&
      !npc.untargetable &&
      Math.abs(npc.level - self.level) <= 1 &&
      npc.healthPercent > 0 &&
      (npc.targetId === 0 || npc.targetId === self.objectId) &&
      distance(home, npc.position) <= LEASH &&
      !hopeless.has(npc.objectId),
  );
  return nearest(agent, mobs);
}

function nearest(agent: Agent, npcs: readonly KnownNpc[]): KnownNpc | undefined {
  const at = agent.self.position;

  if (!at)
    return undefined;

  return [...npcs].sort((a, b) => distance(at, a.position) - distance(at, b.position))[0];
}

/** Releases, waits to come back to life at the bind point, and runs home. */
async function recover(agent: Agent, home: Position): Promise<Outcome> {
  const alive = await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, RELEASE_TIMEOUT_MS);

  if (!alive)
    return failed("notReleased");

  const back = await runTo(agent, home);
  return !back.ok && back.reason === "unreachable" ? failed("homeUnreachable") : succeeded;
}

/** Runs to a random spot within the leash of home, and stops as soon as there's something else to do. */
async function wander(agent: Agent, home: Position, somethingElse: () => boolean): Promise<void> {
  const angle = agent.random() * 2 * Math.PI;
  // Spread evenly over the circle, not bunched at its middle.
  const radius = LEASH * Math.sqrt(agent.random());
  const spot = { x: Math.round(home.x + radius * Math.cos(angle)), y: Math.round(home.y + radius * Math.sin(angle)), z: home.z };
  const ended = await agent.sendAndWait({ name: "moveTo", ...spot }, (percept) => runEnd(percept) ?? (somethingElse() ? "interrupted" : undefined), RUN_TIMEOUT_MS);

  if (ended === "interrupted")
    await agent.send({ name: "stop" });
}

/** What the grind did, for the summary at the end. */
class Tally {
  kills = 0;
  deaths = 0;
  rests = 0;
  readonly #failures = new Map<string, number>();
  readonly #startedMs: number;

  constructor(startedMs: number) {
    this.#startedMs = startedMs;
  }

  killed(outcome: Outcome): void {
    if (outcome.ok)
      this.kills++;

    this.note(outcome);
  }

  note(outcome: Outcome): void {
    if (!outcome.ok)
      this.#failures.set(outcome.reason, (this.#failures.get(outcome.reason) ?? 0) + 1);
  }

  summary(nowMs: number): string {
    const seconds = Math.round((nowMs - this.#startedMs) / 1000);
    const failures = [...this.#failures].map(([reason, count]) => `${reason} ${count}`).join(", ") || "none";
    return `Reached level ${GOAL_LEVEL} in ${Math.floor(seconds / 60)} min ${seconds % 60} s: ${this.kills} kills, ${this.deaths} deaths, ${this.rests} rests. Tactic failures: ${failures}.`;
  }
}
