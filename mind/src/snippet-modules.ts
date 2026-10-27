// Each snippet imports the Agent's files into a module graph of its own, so it gets them as they are on disk now and none of an earlier snippet's modules carry over.
// Bun keeps a module that failed to load cached for good, even once the file is fixed, so the Agent's files can't share the Mind's module registry.
// The Mind's own files are the exception: a snippet gets the very modules the Mind loaded when it started, so `instanceof` and shared objects still work.

import { Glob } from "bun";
import { dirname, resolve, sep } from "node:path";

const MIND_FILES = import.meta.dir;

/** The folders of the Agents whose files import the Mind's own modules. */
const agentFolders = new Set<string>();

/** The Mind's own modules by path, as it loaded them when it started. */
const own = loadOwn();

async function loadOwn(): Promise<Map<string, Record<string, unknown>>> {
  const modules = new Map<string, Record<string, unknown>>();

  for (const file of new Glob("**/*.ts").scanSync(MIND_FILES)) {
    if (!/(^|[\\/])test-|\.test\.ts$/.test(file))
      modules.set(resolve(MIND_FILES, file), await import(resolve(MIND_FILES, file)));
  }

  return modules;
}

Bun.plugin({
  name: "the Mind's own modules",
  setup(build) {
    build.onResolve({ filter: /^(\.\.?[\\/]|[A-Za-z]:[\\/]|\/)/ }, ({ path, importer }) => {
      // The Mind's own files import each other as usual, even when the Agent's folder holds them, as in the Mind's tests.
      if (importer.startsWith(MIND_FILES + sep) || ![...agentFolders].some((folder) => importer.startsWith(folder + sep)))
        return;

      const file = resolve(dirname(importer), path);
      return file.startsWith(MIND_FILES + sep) ? { path: file, namespace: "mind" } : undefined;
    });

    build.onLoad({ filter: /.*/, namespace: "mind" }, async ({ path }) => {
      const exports = (await own).get(path);

      if (!exports)
        throw new Error(`${path} is newer than this Mind, which loaded its own files when it started. Restart the Mind to use it.`);

      return { loader: "object", exports };
    });
  },
});

/** Imports a file by its absolute path. */
export type Import = (path: string) => Promise<unknown>;

/** A snippet's modules: what it imports from the Agent's folder, until it's disposed of. */
export interface SnippetModules extends Disposable {
  import: Import;
  /** Runs `body` so that what it opens, such as timers, belongs to these modules and ends with them. */
  run<T>(body: () => Promise<T>): Promise<T>;
}

/** Fresh modules for a snippet run from the Agent's `folder`. */
export async function snippetModules(folder: string): Promise<SnippetModules> {
  agentFolders.add(resolve(folder));
  const mind = await own;
  const graph = new Bun.ModuleGraph();

  return {
    import: async (path) => mind.get(path) ?? (await graph.import(path)),
    run: (body) => graph.run(body),
    [Symbol.dispose]: () => graph.dispose(),
  };
}
