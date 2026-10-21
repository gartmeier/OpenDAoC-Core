import type { Percept, Primitive } from "./protocol.ts";
import type { Self } from "./self.ts";
import { distance, headingTowards, type Position, type Surroundings } from "./surroundings.ts";

// How a say line reads to those who hear it (`GamePlayer.SayReceive.Says` in the server's English language file): `Vue says, "come"`.
// The speaker is a Character's name, or an NPC's with its article (`the town guard`). The words are as said, quotes included.
// The speaker's own line reads `You say, "come"`, and a yell or a language the listener doesn't understand reads differently, so none of them match.
const SAY_LINE = /^(.+?) says, "(.+)"$/;

/** How far short of the speaker the Agent stops, in game units, so that it stands next to them rather than inside them. */
export const STAND_OFF = 100;

/**
 * What the Come Mind does with a Percept: when someone next to the Agent says `come`, it runs to where they stood when they said it and turns to face them, and `stop` stops it.
 * The message packet has no speaker field, so the speaker and the words are read from the say line's text, and the speaker is found by name in Surroundings.
 * It knows the Agent only through Self and Surroundings, and is the only part of the Mind that's about coming.
 */
export class Come {
  readonly #agent: string;
  // Who the run in progress is coming to, to face once it arrives.
  #speaker: string | undefined;

  /** `agent` is the Agent's Character, in any case. */
  constructor(agent: string) {
    this.#agent = agent.toLowerCase();
  }

  /** The Primitive to send for the Percept, or nothing. */
  decide(percept: Percept, agentSelf: Self, surroundings: Surroundings): Primitive | undefined {
    if (percept.kind === "arrived")
      return this.#arrived(agentSelf, surroundings);

    if (percept.kind !== "message" || percept.chatType !== "CT_Say")
      return undefined;

    const [, speaker, words] = SAY_LINE.exec(percept.text) ?? [];

    if (!speaker || !words || speaker.toLowerCase() === this.#agent)
      return undefined;

    switch (words.toLowerCase()) {
      case "come":
        return this.#comeTo(speaker, agentSelf, surroundings);
      case "stop":
        this.#speaker = undefined;
        return { name: "stop" };
      default:
        return undefined;
    }
  }

  // A speaker the Agent doesn't know of is somewhere it couldn't know, and one already within the stand-off distance is close enough to only turn to.
  #comeTo(speaker: string, agentSelf: Self, surroundings: Surroundings): Primitive | undefined {
    const from = agentSelf.position;
    const to = surroundings.named(speaker)?.position;

    if (!from || !to)
      return undefined;

    const apart = distance(from, to);

    if (apart <= STAND_OFF)
      return face(from, to);

    this.#speaker = speaker;
    const { x, y, z } = shortOf(from, to, apart);
    return { name: "moveTo", x, y, z };
  }

  // The run to a speaker got there: it turns to where the speaker was last known to be, which may not be where they said `come`.
  #arrived(agentSelf: Self, surroundings: Surroundings): Primitive | undefined {
    const speaker = this.#speaker;
    this.#speaker = undefined;
    const from = agentSelf.position;
    const to = speaker === undefined ? undefined : surroundings.named(speaker)?.position;

    if (!from || !to)
      return undefined;

    return face(from, to);
  }
}

/** A `face` towards `to` from `from`, or nothing if `to` is straight above, below or at `from`. */
function face(from: Position, to: Position): Primitive | undefined {
  const heading = headingTowards(from, to);
  return heading === undefined ? undefined : { name: "face", heading };
}

/** The point `STAND_OFF` short of `to`, on the line from `from`, `apart` away from it. */
function shortOf(from: Position, to: Position, apart: number): Position {
  const back = STAND_OFF / apart;
  return { x: to.x + (from.x - to.x) * back, y: to.y + (from.y - to.y) * back, z: to.z + (from.z - to.z) * back };
}
