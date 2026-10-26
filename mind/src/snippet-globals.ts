import { SLOT } from "./inventory.ts";
import { cast } from "./tactics/cast.ts";
import { answer } from "./tactics/dialog.ts";
import { equip } from "./tactics/equip.ts";
import { kill } from "./tactics/kill.ts";
import { rest } from "./tactics/rest.ts";
import { runTo } from "./tactics/run-to.ts";
import { buy, sell, talkTo } from "./tactics/trade.ts";
import { train } from "./tactics/train.ts";
import { whisper } from "./tactics/whisper.ts";

/** What every `execute_code` snippet has at hand besides `agent`: the porcelain Tactics and the slot names. */
export const SNIPPET_GLOBALS = { kill, cast, rest, train, runTo, equip, talkTo, whisper, answer, buy, sell, SLOT };
