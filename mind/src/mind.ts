import { resolve } from "node:path";
import { describePercept, describePrimitive } from "./journal.ts";
import type { Percept, PerceptMessage, Primitive, PrimitiveResultMessage } from "./protocol.ts";
import { Runner } from "./runner.ts";
import { readGroupLine, readSayLine } from "./say-line.ts";
import { SNIPPET_GLOBALS } from "./snippet-globals.ts";
import { type Import, snippetModules } from "./snippet-modules.ts";
import { summarize } from "./summary.ts";
import { Agent, type Clock, failed, type Outcome, Stopped, succeeded, type Tactic } from "./tactic.ts";

export interface MindOptions {
  /** The Agent's Character. */
  character: string;
  /** The Agent's folder, which relative imports in a snippet resolve against. */
  folder: string;
  /** Sends a Primitive to the server. */
  send: (primitive: Primitive) => Promise<PrimitiveResultMessage>;
  /** The system clock unless set. */
  clock?: Clock;
  /** `Math.random` unless set. */
  random?: () => number;
  /** Where the Journal's lines go. */
  journal: (line: string) => void;
  /** Whether the Agent's Character is in the world, and how to enter it again. Always in it unless set. */
  world?: World;
}

export interface World {
  /** Whether the Character is in the world, so Primitives can be sent. */
  inWorld(): boolean;
  /** Enters the world again, or waits for the entry under way, and gives how that went. */
  enter(): Promise<Entry>;
}

/** How entering the world went: in it, by logging in or by a Takeover; failed, and why; or not tried, since another Mind has taken the Agent over. */
export type Entry = { kind: "entered"; tookOver: boolean } | { kind: "failed"; reason: string } | { kind: "replaced" };

export interface ExecuteCodeRequest {
  /** TypeScript, run as the body of an async function. */
  code: string;
  /** In minutes: `DEFAULT_TIMEOUT_MINUTES` unless set, and clamped to between `MIN_TIMEOUT_MINUTES` and `MAX_TIMEOUT_MINUTES`. */
  timeout?: number;
}

export const DEFAULT_TIMEOUT_MINUTES = 2;
export const MIN_TIMEOUT_MINUTES = 0.1;
// Claude Code gives up on an MCP call after 30 minutes, whatever `MCP_TOOL_TIMEOUT` says, and a snippet still running then is lost to it.
export const MAX_TIMEOUT_MINUTES = 25;

/** How many of the chat and game messages since the last call a result lists, newest last. */
export const CHAT_LISTED = 50;

/** What `execute_code` answers: the text the coding agent reads, and whether the snippet failed or was aborted. */
export interface ToolResult {
  text: string;
  isError: boolean;
}

/**
 * What a coding agent plays through (ADR 0009): it runs TypeScript snippets against the Agent, one at a time, and says how each went and what the Agent now believes.
 */
export class Mind {
  readonly #options: MindOptions;
  #agent!: Agent;
  #runner!: Runner;
  #execution: Execution | undefined;
  /** The chat and game messages since the last call. */
  #chat: string[] = [];
  /** The Group member whose line in group chat ends the snippet running at its next safe point (ADR 0010). */
  #groupSpeaker: string | undefined;

  constructor(options: MindOptions) {
    this.#options = options;
    this.forget();
  }

  get agent(): Agent {
    return this.#agent;
  }

  /** Starts the Agent's beliefs afresh, as when its Character logs in again: what it believed before may be long gone. */
  forget(): void {
    const { character, send, clock, random, journal } = this.#options;
    const agent = new Agent({
      send: (primitive) => {
        journal(`Sending ${describePrimitive(agent, character, primitive)}.`);
        return send(primitive);
      },
      clock,
      random,
      log: journal,
    });
    this.#agent = agent;
    this.#runner = new Runner(agent);
  }

  /**
   * Takes in a Percept. A `stop` someone says next to the Agent aborts the snippet running.
   * A Group member's line in group chat ends it too, once nothing it sees is fighting the Agent, a Group member or its Pet, and it isn't casting.
   */
  perceive(message: PerceptMessage): void {
    this.agent.perceive(message);
    this.#options.journal(describePercept(this.agent, this.#options.character, message));

    if (message.percept.kind === "message")
      this.#chat.push(`${message.percept.chatType}\t${message.percept.text}`);

    if (!this.#execution)
      return;

    const speaker = heardStop(message.percept, this.#options.character);

    if (speaker) {
      void this.#abort(`a stop ${speaker} said`);
      return;
    }

    const groupLine = readGroupLine(message.percept);

    if (groupLine && groupLine.speaker.toLowerCase() !== this.#options.character.toLowerCase())
      this.#groupSpeaker ??= groupLine.speaker;

    if (this.#groupSpeaker && betweenFights(this.agent))
      void this.#abort(`a line ${this.#groupSpeaker} said in group chat`);
  }

  /** The `execute_code` tool. */
  async executeCode({ code, timeout = DEFAULT_TIMEOUT_MINUTES }: ExecuteCodeRequest): Promise<ToolResult> {
    await this.#abort("a new call");
    const { character, world } = this.#options;
    const lines: string[] = [];
    const started = this.agent.clock.now();
    let ending: SnippetEnding;

    if (world && !world.inWorld()) {
      const entry = await world.enter();

      if (entry.kind === "replaced")
        return { text: `Another Mind has taken ${character} over, and this one won't take it back.\n`, isError: true };

      if (entry.kind === "failed")
        return { text: `${character} isn't in the world, and entering it failed: ${entry.reason}.\n`, isError: true };

      lines.push(entry.tookOver ? `${character} was in the world under another Mind, and this Mind has taken it over first.` : `${character} wasn't in the world, and has entered it first.`);
    }

    try {
      // Disposed of when the snippet ends, with whatever it left running.
      using modules = await snippetModules(this.#options.folder);
      const snippet = compile(code, this.#options.folder);
      const execution = new Execution(this.agent);
      this.#execution = execution;
      ending = { error: new Error("The snippet didn't run.") };
      const minutes = Math.min(Math.max(timeout, MIN_TIMEOUT_MINUTES), MAX_TIMEOUT_MINUTES);
      const cancelTimeout = this.agent.clock.schedule(minutes * 60_000, () => {
        if (this.#execution === execution)
          void this.#abort(`its timeout of ${minutes} min`);
      });

      this.#options.journal(`Snippet, with a timeout of ${minutes} min:\n${code.replace(/^/gm, "  ")}`);

      await this.#runner.start("snippet", async () => {
        const restore = captureConsole(lines);

        try {
          ending = await Promise.race([
            modules.run(() => snippet(execution.agent, consoleInto(lines), modules.import)).then(
              (value): SnippetEnding => ({ value }),
              (error): SnippetEnding => ({ error }),
            ),
            execution.aborted.then((reason): SnippetEnding => ({ aborted: reason })),
          ]);
        } finally {
          restore();
        }

        if ("value" in ending)
          return succeeded;

        return failed("aborted" in ending ? `aborted by ${ending.aborted}` : describeError(ending.error).split("\n")[0]!);
      });

      cancelTimeout();
      // Whatever the snippet left running is aborted with it, though what it already sent may still take effect.
      execution.abort("its end");

      if (this.#execution === execution)
        this.#execution = undefined;
    } catch (error) {
      ending = { error };
    }

    const took = seconds(this.agent.clock.now() - started);
    const parts = [`Console:\n${bounded(lines).join("\n")}\n`];

    if ("value" in ending)
      parts.push(`Result:\n${describeValue(ending.value)}\n`);
    else if ("aborted" in ending)
      parts.push(`Aborted by ${ending.aborted} after ${took}.\n`);
    else
      parts.push(`Error after ${took}:\n${describeError(ending.error)}\n`);

    parts.push(`State:\n${summarize(this.agent, this.#options.character)}\n`);
    const chat = this.#chat.splice(0);

    if (chat.length > CHAT_LISTED)
      parts.push(`Chat (newest ${CHAT_LISTED} of ${chat.length}):\n${chat.slice(-CHAT_LISTED).join("\n")}\n`);
    else
      parts.push(chat.length ? `Chat:\n${chat.join("\n")}\n` : "Chat: nothing since the last call.\n");

    return { text: parts.join("\n"), isError: !("value" in ending) };
  }

  /** Aborts the snippet running, if one is, before the Mind quits the Agent. */
  async close(): Promise<void> {
    await this.#abort("the Mind closing");
  }

  /** Aborts the snippet running, if one is, and leaves the Agent standing with attack off. */
  async #abort(reason: string): Promise<void> {
    this.#execution?.abort(reason);
    this.#execution = undefined;
    this.#groupSpeaker = undefined;
    await this.#runner.stop();
  }
}

/** Who said `stop` next to the Agent, in any case, or nothing. */
function heardStop(percept: Percept, character: string): string | undefined {
  if (percept.kind !== "message" || percept.chatType !== "CT_Say")
    return undefined;

  const { speaker, words } = readSayLine(percept.text) ?? {};
  return speaker && words?.toLowerCase() === "stop" && speaker.toLowerCase() !== character.toLowerCase() ? speaker : undefined;
}

/** Whether no Mob the Agent sees is attacking it, a Group member or its Pet, and it isn't casting. */
function betweenFights({ self, surroundings, group }: Agent): boolean {
  const ours = [self.objectId, self.pet?.objectId, ...group.members.map((member) => member.objectId)].filter((objectId) => objectId !== undefined);
  return !self.casting && ours.every((objectId) => surroundings.attacking(objectId).length === 0);
}

type SnippetEnding = { value: unknown } | { error: unknown } | { aborted: string };

/** A promise that never settles. */
const NEVER = new Promise<never>(() => {});

/**
 * One snippet's run, with its own handle on the Agent. Once it's aborted, the handle's sends and waits never come back, as rs-sdk's do,
 * so a snippet that swallows every error can't carry on, or spin, once the Agent runs the next one.
 */
class Execution {
  readonly agent: Agent;
  readonly aborted: Promise<string>;
  #reason: string | undefined;
  #resolveAborted!: (reason: string) => void;

  constructor(agent: Agent) {
    this.aborted = new Promise((resolve) => (this.#resolveAborted = resolve));
    const guard = <T>(start: () => Promise<T>): Promise<T> => {
      if (this.#reason !== undefined)
        return NEVER;

      return start().then(
        (value) => (this.#reason === undefined ? value : NEVER),
        (error) => (this.#reason === undefined ? Promise.reject(error) : NEVER),
      );
    };
    const handle: Agent = new Proxy(agent, {
      get(target, property) {
        switch (property) {
          case "send":
            return (primitive: Primitive) => guard(() => target.send(primitive));
          case "waitFor":
            return (...args: Parameters<Agent["waitFor"]>) => guard(() => target.waitFor(...args));
          case "sendAndWait":
            return (...args: Parameters<Agent["sendAndWait"]>) => guard(() => target.sendAndWait(...args));
          case "run":
            // A Tactic it runs gets the handle too.
            return (tactic: Tactic<unknown[]>, ...args: unknown[]) => guard(() => runOn(handle, tactic, args));
        }

        const value = Reflect.get(target, property);
        return typeof value === "function" ? value.bind(target) : value;
      },
    });
    this.agent = handle;
  }

  abort(reason: string): void {
    if (this.#reason !== undefined)
      return;

    this.#reason = reason;
    this.#resolveAborted(reason);
  }
}

/** As `Agent.run` does, on the handle. */
async function runOn(agent: Agent, tactic: Tactic<unknown[]>, args: unknown[]): Promise<Outcome> {
  try {
    return await tactic(agent, ...args);
  } catch (error) {
    if (error instanceof Stopped)
      return failed("stopped");

    throw error;
  }
}

function seconds(ms: number): string {
  return `${Number((ms / 1000).toFixed(1))} s`;
}

/** A snippet's result as JSON, with maps as objects and sets as arrays, or as Bun prints it when JSON can't hold it. */
function describeValue(value: unknown): string {
  try {
    return String(JSON.stringify(value, (_, inner: unknown) => (inner instanceof Map ? Object.fromEntries(inner) : inner instanceof Set ? [...inner] : inner), 2));
  } catch {
    return Bun.inspect(value);
  }
}

/** An error's message and the first lines of its stack. */
function describeError(error: unknown): string {
  if (!(error instanceof Error))
    return String(error);

  const stack = (error.stack ?? "").split("\n").filter((line) => line.trim().startsWith("at ")).slice(0, 7);
  return [`${error.name}: ${error.message}`, ...stack].join("\n");
}

const transpiler = new Bun.Transpiler({ loader: "ts" });

type Snippet = (agent: Agent, console: Console, imports: Import) => Promise<unknown>;

// A dynamic import of a relative path, such as `import("./tactics/hunt.ts")`.
const RELATIVE_IMPORT = /\bimport\(\s*(["'])(\.\.?\/[^"']*)\1\s*\)/g;

/** The snippet as a function. Its relative imports resolve against `folder`, not against the Mind's own files, and go through `imports`. */
function compile(code: string, folder: string): Snippet {
  // A plain `import()` here would load into the Mind's own module registry, even inside the snippet's module graph.
  const imports = code.replace(RELATIVE_IMPORT, (_, _quote: string, path: string) => `__import(${JSON.stringify(resolve(folder, path))})`);
  const js = transpiler.transformSync(`const __snippet = async (agent, console, __import, ${Object.keys(SNIPPET_GLOBALS).join(", ")}) => {\n${imports}\n};`);
  const snippet = new Function(`${js}\nreturn __snippet;`)();
  return (agent, console, imports) => snippet(agent, console, imports, ...Object.values(SNIPPET_GLOBALS));
}

/** How much of a snippet's console output a result keeps: its first and last lines, each cut short. */
export const CONSOLE_LINES_KEPT = 300;
export const CONSOLE_LINE_LENGTH = 2000;

function bounded(lines: string[]): string[] {
  const cut = lines.map((line) => (line.length > CONSOLE_LINE_LENGTH ? `${line.slice(0, CONSOLE_LINE_LENGTH)}…` : line));

  if (cut.length <= 2 * CONSOLE_LINES_KEPT)
    return cut;

  return [...cut.slice(0, CONSOLE_LINES_KEPT), `… ${cut.length - 2 * CONSOLE_LINES_KEPT} lines left out …`, ...cut.slice(-CONSOLE_LINES_KEPT)];
}

const CONSOLE_METHODS = ["log", "info", "warn", "error", "debug"] as const;

/**
 * Sends what the files a snippet imports log to `lines` while it runs, and gives what puts the console back.
 * The snippet itself logs through its own console, `consoleInto(lines)`, so what it logs after it was aborted stays out of the next snippet's lines.
 */
function captureConsole(lines: string[]): () => void {
  const saved = CONSOLE_METHODS.map((method) => console[method]);
  const into = consoleInto(lines);

  for (const method of CONSOLE_METHODS)
    console[method] = into[method];

  return () => CONSOLE_METHODS.forEach((method, i) => (console[method] = saved[i]!));
}

/** A console whose logging goes to `lines`. */
function consoleInto(lines: string[]): Console {
  const log = (...args: unknown[]) => void lines.push(args.map((arg) => (typeof arg === "string" ? arg : Bun.inspect(arg))).join(" "));
  return { ...console, log, info: log, warn: log, error: log, debug: log };
}
