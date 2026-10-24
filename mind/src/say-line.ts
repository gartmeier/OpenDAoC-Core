import type { Percept } from "./protocol.ts";

// How a say line reads to those who hear it (`GamePlayer.SayReceive.Says`, `GameNPC.SayTo.Says` and `GameTrainer.PromotePlayer.Says` in the server's English language files): `Vue says, "stop"`.
// The speaker is a Character's name, or an NPC's with its article (`The town guard`). The Agent's own line reads `You say, "stop"`, so it doesn't match.
// A message packet has no speaker field, so the speaker is read from the text, as a Human reading the client does.
const SAY_LINE = /^(.+?) says, "([\s\S]+)"$/;

/** Who said what in a say line, or nothing for a message that isn't one. */
export function readSayLine(text: string): { speaker: string; words: string } | undefined {
  const [, speaker, words] = SAY_LINE.exec(text) ?? [];
  return speaker && words ? { speaker, words } : undefined;
}

/** Whether the Percept is words the NPC named `name` said, such as `Ea says, "…"`, in any case and with or without its article. */
export function saidBy(name: string, percept: Percept | undefined): boolean {
  if (percept?.kind !== "message")
    return false;

  const speaker = readSayLine(percept.text)?.speaker.toLowerCase();
  const npc = name.toLowerCase();
  return speaker === npc || speaker === `the ${npc}`;
}
