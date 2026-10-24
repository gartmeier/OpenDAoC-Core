// The Mind as a stdio MCP server, which a coding agent such as Claude Code starts and plays through (ADR 0009).
// It logs the Agent's Character in when it starts and keeps it in the world until its input closes, then quits it through the normal quit path.
// Its one tool, `execute_code`, runs a TypeScript snippet against the Agent (see `src/mind.ts`).
// The Agent's folder is the working directory: snippets import from it, and the Journal is written to its `journal` folder.
// Usage: bun mind/mcp.ts <character>
// The server address and token come from mind/.env (see mind/.env.example).

import { createWriteStream } from "node:fs";
import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { Connection, type Ending } from "./src/connection.ts";
import { loadEnvFile } from "./src/env.ts";
import { DEFAULT_TIMEOUT_MINUTES, type Entry, MAX_TIMEOUT_MINUTES, Mind, MIN_TIMEOUT_MINUTES } from "./src/mind.ts";

const character = Bun.argv[2] ?? "";

if (!character) {
  console.error("Usage: bun mind/mcp.ts <character>");
  process.exit(2);
}

await loadEnvFile(new URL(".env", import.meta.url));
const url = process.env.MIND_SERVER_URL || "ws://localhost:10310/mind";
const token = process.env.MIND_TOKEN ?? "";

if (!token) {
  console.error("MIND_TOKEN isn't set. Copy mind/.env.example to mind/.env and set it to the server property agent_mind_token.");
  process.exit(2);
}

// Stdout carries only MCP messages, so whatever else is logged goes to stderr.
console.log = console.info = console.debug = console.error;

const folder = process.cwd();
await mkdir(join(folder, "journal"), { recursive: true });
const journalFile = createWriteStream(join(folder, "journal", `${new Date().toISOString().replace(/[:.]/g, "-")}.log`), { flags: "a" });
const journal = (line: string) => journalFile.write(`${line}\n`);

let connection: Connection | undefined;
let ending: Ending | undefined;
let connected: Promise<Ending> | undefined;
let entering: Promise<Entry> | undefined;

const mind = new Mind({
  character,
  folder,
  send: (primitive) => connection!.send(primitive),
  journal,
  world: { inWorld: () => ending === undefined && connection?.entered === true, enter },
});

/** Logs the Character in, or waits for the login already under way, and gives how that went. Once another Mind has taken the Agent over, this one doesn't try. */
function enter(): Promise<Entry> {
  if (ending?.kind === "replaced")
    return Promise.resolve({ kind: "replaced" });

  entering ??= login().finally(() => (entering = undefined));
  return entering;
}

function login(): Promise<Entry> {
  // After a lost connection, what the Agent believed may be long gone, such as the objects it saw before a server restart.
  if (connection)
    mind.forget();

  return new Promise((resolve) => {
    ending = undefined;
    const next = new Connection({ url, token, character, log: journal }, {
      onPercept: (message) => mind.perceive(message),
      onEntered: (_, tookOver) => resolve({ kind: "entered", tookOver }),
    });
    connection = next;
    connected = next.run({ handleCtrlC: false }).then((end) => {
      ending = end;
      resolve(end.kind === "replaced" ? { kind: "replaced" } : { kind: "failed", reason: describe(end) });
      return end;
    });
  });
}

function describe(end: Ending): string {
  switch (end.kind) {
    case "left":
      return "the server disconnected it";
    case "refused":
      return `the server refused it: ${end.reason}`;
    case "replaced":
      return "another Mind took over the Agent";
    case "lost":
      return `the connection was lost (${end.code}${end.reason ? ` ${end.reason}` : ""})`;
  }
}

const server = new McpServer({ name: "mind", version: "1.0.0" });

server.registerTool(
  "execute_code",
  {
    description: [
      `Runs TypeScript against ${character}, the Agent you play, as the body of an async function with top-level await, and returns its console output, its result or error, what the Agent believes about itself, what's near it and what it carries, and the chat since the last call.`,
      "In scope: `agent` (its Primitives, Self, Surroundings, Inventory and waits), the porcelain Tactics `kill`, `rest`, `train`, `runTo`, `equip`, `talkTo`, `whisper`, `buy` and `sell`, and the equipment slot numbers `SLOT`. Import your own files with `await import(\"./tactics/x.ts\")`, relative to your folder.",
      "One snippet runs at a time: a new call aborts the one running, as does its timeout or a `stop` said next to the Agent, and the Agent is left standing with attack off.",
    ].join(" "),
    inputSchema: {
      code: z.string().describe("The snippet."),
      timeout: z.number().optional().describe(`In minutes: ${DEFAULT_TIMEOUT_MINUTES} unless set, at least ${MIN_TIMEOUT_MINUTES} and at most ${MAX_TIMEOUT_MINUTES}.`),
    },
  },
  async ({ code, timeout }) => {
    const { text, isError } = await mind.executeCode({ code, timeout });
    return { content: [{ type: "text", text }], isError };
  },
);

let closing = false;

/** Stops any snippet, quits the Character through the normal quit path, and exits once the server has disconnected it. */
async function close(): Promise<void> {
  if (closing)
    return;

  closing = true;
  await mind.close();

  if (ending === undefined && connection) {
    connection.interrupt();
    await connected;
  }

  journalFile.end(() => process.exit(0));
}

process.stdin.on("end", () => void close());
process.stdin.on("close", () => void close());
process.on("SIGINT", () => void close());
process.on("SIGTERM", () => void close());

void enter();
await server.connect(new StdioServerTransport());
