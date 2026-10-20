const CTRL_C = 0x03;

/**
 * Calls `handler` on every Ctrl+C until the returned function is called.
 * In a terminal it reads Ctrl+C as a key, with stdin in raw mode, instead of handling the console's Ctrl+C signal.
 * On Windows, PowerShell exits when a program it runs survives that signal, and closing the terminal kills the program with it, so a Mind could never wait out a quit delay.
 * Without a terminal it falls back to SIGINT.
 */
export function onCtrlC(handler: () => void): () => void {
  const stdin = process.stdin;

  if (!stdin.isTTY) {
    process.on("SIGINT", handler);
    return () => process.off("SIGINT", handler);
  }

  const onData = (data: Buffer) => {
    if (data.includes(CTRL_C))
      handler();
  };

  stdin.setRawMode(true);
  stdin.on("data", onData);

  return () => {
    stdin.off("data", onData);
    stdin.setRawMode(false);
    stdin.pause();
  };
}
