import type { Percept } from "../protocol.ts";
import { LANDING_MARGIN_MS } from "../self.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** In milliseconds: how long the server has to answer a cast at all, by starting it, landing it or refusing it. Also how long Quick Cast has to come up. */
export const START_TIMEOUT_MS = 3_000;

/** How many times it casts while the server says to wait. */
const MAX_CASTS = 3;

/** Past the N seconds the server says to wait, which it rounds down. */
const WAIT_MARGIN_MS = 300;

/** The refusals and failures the server gives only as chat, all as `CT_SpellResisted`, with the reason each ends the cast with. Any other is `refused`. */
const REASONS: [RegExp, string][] = [
  [/too far away|out of range/, "tooFar"],
  [/can't see your target|not visible/, "notInView"],
  [/select a target/, "noTarget"],
  [/enough power|exhausted all of your power/, "noPower"],
];

/** "You must wait N seconds to cast a spell!" after a swing, "... to recast this type of spell!" on Cooldown, or "You need to wait N more seconds ..." after a potion. */
const WAIT = /wait (\d+) (?:more )?seconds?/;

type Answer = { kind: "started"; castingMs: number } | { kind: "wait"; seconds: number } | { kind: "ended"; outcome: Outcome };

/**
 * Casts `spell` at `targetId`, the Agent's own object ID for a heal or a buff on itself, and ends once it has landed (ADR 0008).
 * Before sending anything it fails with `noSpell`, `cooldown`, `noPower` or `wrongTarget` (a friendly Spell at a Mob). It stands up, stops a run and targets first.
 * With `quickCast` it turns attack off and uses Quick Cast first, failing with `noQuickCast` if it doesn't come up.
 * It succeeds once the Spell lands, a bolt at launch, and fails with `noEffect` when it doesn't take, with `interrupted`, or with the server's refusal: `tooFar`, `notInView`, `noTarget`, `noPower` or any other as `refused`.
 * It waits out "You must wait N seconds" and casts again, failing with `mustWait` after the third, and fails with `timeout` when the server doesn't answer.
 */
export async function cast(agent: Agent, spell: string, targetId: number, { quickCast = false }: { quickCast?: boolean } = {}): Promise<Outcome> {
  const { self, surroundings } = agent;
  const known = self.skills.find((skill) => skill.page === "Spells" && skill.name === spell);
  const details = known?.page === "Spells" ? known.details : undefined;

  if (!known)
    return failed("noSpell");

  if (self.cooldowns.has(spell) || (quickCast && self.cooldowns.has("Quickcast")))
    return failed("cooldown");

  if ((details?.powerCost ?? 0) > self.power)
    return failed("noPower");

  // The server drops a friendly Spell aimed at a Mob without a word.
  if (/^(Realm|GROUP)/i.test(details?.target ?? "") && surroundings.npcs.some((npc) => npc.objectId === targetId && npc.realm === "None"))
    return failed("wrongTarget");

  if (self.sitting)
    await agent.send({ name: "stand" });

  // Moving interrupts the cast.
  if (self.movement.kind === "moving")
    await agent.send({ name: "stop" });

  await agent.send({ name: "target", objectId: targetId });

  // Using Quick Cast while it's up would turn it off. The server refuses it while attacking.
  if (quickCast && !quickCastUp(agent)) {
    await agent.send({ name: "attack", on: false });

    if (!(await agent.sendAndWait({ name: "useAbility", ability: "Quickcast" }, () => quickCastUp(agent), START_TIMEOUT_MS)))
      return failed("noQuickCast");
  }

  for (let casts = 1; ; casts++) {
    let answer = await agent.sendAndWait({ name: "castSpell", spell }, (percept) => answerTo(agent, spell, percept), START_TIMEOUT_MS);

    // A start says how long the cast takes: it lands then, or as it ends fails with only a message.
    while (answer?.kind === "started")
      answer = await agent.waitFor((percept) => answerTo(agent, spell, percept), answer.castingMs + LANDING_MARGIN_MS);

    if (!answer)
      return failed("timeout");

    if (answer.kind === "ended")
      return answer.outcome;

    if (casts === MAX_CASTS)
      return failed("mustWait");

    await agent.waitFor(() => undefined, answer.seconds * 1000 + WAIT_MARGIN_MS);
  }
}

function quickCastUp(agent: Agent): boolean {
  return agent.self.effects.some((effect) => effect.name === "Quick Cast");
}

/** What `percept` says about the Agent's own cast of `spell`, if anything. */
function answerTo(agent: Agent, spell: string, percept: Percept | undefined): Answer | undefined {
  const me = agent.self.objectId;

  switch (percept?.kind) {
    case "castStarted":
      return percept.casterId === me ? { kind: "started", castingMs: percept.castingMs } : undefined;
    case "spellLanded":
      return percept.casterId === me ? { kind: "ended", outcome: percept.success ? succeeded : failed("noEffect") } : undefined;
    case "interrupted":
      return percept.objectId === me ? { kind: "ended", outcome: failed("interrupted") } : undefined;
    case "message": {
      const wait = WAIT.exec(percept.text);

      if (wait && /cast/.test(percept.text))
        return { kind: "wait", seconds: Number(wait[1]) };

      // A resist is no refusal: the landing says it didn't take. The Agent's Pet's resists and others' come as the same line.
      if (percept.chatType !== "CT_SpellResisted" || /resists? the effect/.test(percept.text))
        return undefined;

      const reason = REASONS.find(([pattern]) => pattern.test(percept.text))?.[1];

      if (!reason)
        agent.log(`The server refused ${spell}: ${percept.text}`);

      return { kind: "ended", outcome: failed(reason ?? "refused") };
    }
    default:
      return undefined;
  }
}
