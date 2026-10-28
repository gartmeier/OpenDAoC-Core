import { expect, test } from "bun:test";
import { mkdtemp, readFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { openJournal } from "./journal.ts";
import { snippetModules } from "./snippet-modules.ts";

test("the Journal file keeps every line written after a snippet that wrote to it ends", async () => {
  const path = join(await mkdtemp(join(tmpdir(), "journal-")), "journal.log");
  const journal = openJournal(path);
  journal.write("before");
  await Bun.sleep(100);

  // As a snippet does when it sends a Primitive: the line is written while its module graph runs, which ends with it.
  const modules = await snippetModules(import.meta.dir);
  await modules.run(async () => {
    for (let i = 0; i < 50; i++)
      journal.write(`inside ${i}`);
  });
  modules[Symbol.dispose]();

  for (let i = 0; i < 20; i++) {
    journal.write(`after ${i}`);
    await Bun.sleep(5);
  }

  journal.close();
  const lines = (await readFile(path, "utf8")).trimEnd().split("\n");
  expect(lines).toHaveLength(71);
  expect(lines.at(-1)).toBe("after 19");
});
