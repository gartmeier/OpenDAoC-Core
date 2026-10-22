import type { Percept } from "./protocol.ts";

// How a say line reads to those who hear it (`GamePlayer.SayReceive.Says` in the server's English language file): `Vue says, "come"`.
// The speaker is a Character's name, or an NPC's with its article (`the town guard`). The words are as said, quotes included.
// The speaker's own line reads `You say, "come"`, and a yell or a language the listener doesn't understand reads differently, so none of them match.
const SAY_LINE = /^(.+?) says, "(.+)"$/;

/** What a Human can tell the Agent by saying it next to it. */
export type CommandWord = "come" | "grind" | "stop";

const WORDS: readonly string[] = ["come", "grind", "stop"] satisfies CommandWord[];

export interface Command {
  readonly speaker: string;
  readonly word: CommandWord;
}

/**
 * The command someone said next to the Agent, in any case, or nothing.
 * The message packet has no speaker field, so the speaker and the words are read from the say line's text. The Agent's own lines aren't commands.
 */
export function heardCommand(percept: Percept, agent: string): Command | undefined {
  if (percept.kind !== "message" || percept.chatType !== "CT_Say")
    return undefined;

  const [, speaker, words] = SAY_LINE.exec(percept.text) ?? [];
  const word = words?.toLowerCase();

  if (!speaker || !word || speaker.toLowerCase() === agent.toLowerCase() || !WORDS.includes(word))
    return undefined;

  return { speaker, word: word as CommandWord };
}
