// Every snippet in the Learnings must compile as an `execute_code` snippet, so that a Learning never teaches code that doesn't work, as rs-sdk checks its learnings.
import { expect, test } from "bun:test";
import { mkdtemp, readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import ts from "typescript";
import { SNIPPET_GLOBALS } from "./snippet-globals.ts";

const LEARNINGS = join(import.meta.dir, "..", "..", "agents", "learnings");
const FENCE = /```ts\n([\s\S]*?)```/g;

test("every snippet in the Learnings typechecks as an execute_code snippet", async () => {
  // Inside `src`, so that the snippets resolve the Mind's modules and types as its own files do.
  const folder = await mkdtemp(join(import.meta.dir, ".learnings-"));

  try {
    const files: string[] = [];

    for (const name of (await readdir(LEARNINGS)).filter((file) => file.endsWith(".md"))) {
      const markdown = (await Bun.file(join(LEARNINGS, name)).text()).replace(/\r\n/g, "\n");
      let n = 0;

      for (const [, code] of markdown.matchAll(FENCE)) {
        const file = join(folder, `${name.replace(/\.md$/, "")}-${++n}.ts`);
        await Bun.write(file, asSnippet(code!));
        files.push(file);
      }
    }

    expect(files.length).toBeGreaterThan(0);
    const { config } = ts.readConfigFile(join(import.meta.dir, "..", "tsconfig.json"), ts.sys.readFile);
    const { options } = ts.parseJsonConfigFileContent(config, ts.sys, join(import.meta.dir, ".."));
    const program = ts.createProgram(files, options);
    const errors = ts
      .getPreEmitDiagnostics(program)
      .filter((diagnostic) => diagnostic.file && files.some((file) => diagnostic.file!.fileName.endsWith(file.replace(/\\/g, "/").split("/").pop()!)))
      .map((diagnostic) => `${diagnostic.file!.fileName.split("/").pop()}: ${ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")}`);

    expect(errors).toEqual([]);
  } finally {
    await rm(folder, { recursive: true, force: true });
  }
});

// The snippet as `execute_code` runs it: the body of an async function of the Agent, with the porcelain at hand.
function asSnippet(code: string): string {
  return [
    `import type { Agent } from "../tactic.ts";`,
    `import { SNIPPET_GLOBALS } from "../snippet-globals.ts";`,
    `const { ${Object.keys(SNIPPET_GLOBALS).join(", ")} } = SNIPPET_GLOBALS;`,
    `export async function snippet(agent: Agent) {`,
    code,
    `}`,
  ].join("\n");
}
