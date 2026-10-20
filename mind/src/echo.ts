import type { Percept, SayPrimitive } from "./protocol.ts";

// How a say line reads to those who hear it (`GamePlayer.SayReceive.Says` in the server's English language file): `Vue says, "hello"`.
// The speaker is a Character's name, or an NPC's with its article (`the town guard`). The words are as said, quotes included.
// The speaker's own line reads `You say, "hello"`, and a yell or a language the listener doesn't understand reads differently, so none of them match.
const SAY_LINE = /^(.+?) says, "(.+)"$/;

/**
 * What the echo Mind does with a Percept: says back what someone else said next to the Agent, or nothing.
 * The message packet has no speaker field, so the speaker and the words are read from the say line's text.
 * `agent` is the Agent's Character, in any case.
 */
export function echo(percept: Percept, agent: string): SayPrimitive | undefined {
  if (percept.kind !== "message" || percept.chatType !== "CT_Say")
    return undefined;

  const [, speaker, words] = SAY_LINE.exec(percept.text) ?? [];

  if (!speaker || !words || speaker.toLowerCase() === agent.toLowerCase())
    return undefined;

  return { name: "say", text: words };
}
