/**
 * Sets the variables in a `KEY=VALUE` file that aren't set already. Blank lines and `#` comments are skipped, and a missing file is fine.
 * Bun only loads `.env` from the working directory, and the Mind is started from the repository root.
 */
export async function loadEnvFile(path: URL): Promise<void> {
  const file = Bun.file(path);

  if (!(await file.exists()))
    return;

  for (const line of (await file.text()).split(/\r?\n/)) {
    const match = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/.exec(line);

    if (!match)
      continue;

    const [, key = "", value = ""] = match;
    process.env[key] ??= value.replace(/^(["'])(.*)\1$/, "$2");
  }
}
