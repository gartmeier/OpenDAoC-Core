import { onCtrlC } from "./ctrl-c.ts";
import type { MindMessage, PerceptMessage, Primitive, PrimitiveResultMessage, ServerMessage } from "./protocol.ts";

export interface ConnectionOptions {
  /** The server's Mind endpoint, for example `ws://localhost:10310/mind`. */
  url: string;
  /** The server property `agent_mind_token`. */
  token: string;
  /** The Agent's Character. */
  character: string;
  /** Where status lines for the Human go. */
  log: (line: string) => void;
  /** How long to wait before trying again after a refusal that says to retry. 5 s unless set. */
  retryDelayMs?: number;
}

export interface ConnectionHandlers {
  /** Every Percept, in the order the server made the calls. Some arrive before `onEntered`, while the Character enters the world. */
  onPercept?: (message: PerceptMessage, connection: Connection) => void;
  /** The Character is in the world, so Primitives can be sent. */
  onEntered?: (connection: Connection) => void;
}

/** How the connection ended. */
export type Ending =
  /** The server disconnected the Agent after it was in the world: a quit, or a kick. */
  | { kind: "left" }
  /** Refused for a reason that retrying can't fix, or refused and then stopped while waiting to retry. */
  | { kind: "refused"; reason: string }
  /** Another Mind took over the Agent's connection. The Character stays in the world. */
  | { kind: "replaced" }
  /** Anything else: the server was unreachable, stopped, or dropped the connection. */
  | { kind: "lost"; code: number; reason: string };

/**
 * One Agent's connection to the server, which is the Agent's network connection (ADR 0005).
 * It logs the Character in, hands Percepts to a handler and sends Primitives. It knows nothing about what the Mind decides.
 * While a refusal says to retry, for example because the Character is still linkdead, it tries again every few seconds.
 * Ctrl+C quits the Agent through the normal quit path and waits for the server to disconnect it. A second Ctrl+C drops the connection, which leaves the Character linkdead.
 * Ctrl+C while waiting to retry stops retrying.
 */
export class Connection {
  readonly #options: ConnectionOptions;
  readonly #handlers: ConnectionHandlers;
  readonly #pending = new Map<number, (result: PrimitiveResultMessage) => void>();
  #socket: WebSocket | undefined;
  #nextId = 1;
  #entered = false;
  #quitting = false;
  #refusal: { reason: string; retry: boolean } | undefined;
  #replaced = false;
  #stopped = false;
  #stopWaiting: (() => void) | undefined;

  constructor(options: ConnectionOptions, handlers: ConnectionHandlers = {}) {
    this.#options = options;
    this.#handlers = handlers;
  }

  /** Whether the Character has entered the world. */
  get entered(): boolean {
    return this.#entered;
  }

  /**
   * Connects, sends the hello, and runs until the connection ends, connecting again while a refusal says to retry.
   * Handles Ctrl+C while it runs, unless `handleCtrlC` is false.
   */
  async run({ handleCtrlC = true }: { handleCtrlC?: boolean } = {}): Promise<Ending> {
    const { character, log, retryDelayMs = 5000 } = this.#options;
    const stopHandlingCtrlC = handleCtrlC ? onCtrlC(() => this.interrupt()) : () => {};

    try {
      while (true) {
        const ending = await this.#connect();

        if (ending.kind !== "refused" || !this.#refusal?.retry || this.#stopped) {
          log(describe(ending, character));
          return ending;
        }

        log(`Refused: ${ending.reason} Retrying in ${retryDelayMs / 1000} s. Ctrl+C stops.`);
        if (!this.#stopped)
          await this.#wait(retryDelayMs);

        if (this.#stopped) {
          log("Stopped retrying.");
          return ending;
        }
      }
    } finally {
      stopHandlingCtrlC();
    }
  }

  /** Sends a Primitive. Its result says only whether it was queued, never whether it worked (ADR 0004). */
  send(primitive: Primitive): Promise<PrimitiveResultMessage> {
    const id = this.#nextId++;

    return new Promise((resolve) => {
      this.#pending.set(id, resolve);
      this.#write({ type: "primitive", id, primitive });
    });
  }

  /**
   * What Ctrl+C does. In the world, the first quits the Agent. Otherwise, or the second time, it drops the connection.
   * Dropping it while in the world makes the Character go linkdead, as a crashed client would.
   */
  interrupt(): void {
    const { character, log } = this.#options;

    if (!this.#entered || this.#quitting) {
      this.#stopped = true;

      // Waiting to retry, with no connection to drop.
      if (this.#stopWaiting) {
        this.#stopWaiting();
        return;
      }

      log(this.#entered ? `Dropping the connection. ${character} goes linkdead.` : "Dropping the connection.");
      this.#socket?.close(1000);
      return;
    }

    this.#quitting = true;
    log(`${character} is quitting. Waiting for the server to disconnect ${character}. Ctrl+C again drops the connection instead.`);

    void this.send({ name: "quit" }).then((result) => {
      if (!result.queued)
        log(`The quit wasn't sent: ${result.reason}`);
    });
  }

  /** One connection, from connecting until it closes. */
  #connect(): Promise<Ending> {
    const { url, token, character, log } = this.#options;
    this.#entered = false;
    this.#quitting = false;
    this.#refusal = undefined;
    this.#replaced = false;

    return new Promise((resolve) => {
      const socket = new WebSocket(url);
      this.#socket = socket;
      log(`Connecting to ${url} to log ${character} in...`);

      socket.addEventListener("open", () => this.#write({ type: "hello", token, character }));
      socket.addEventListener("message", (event) => this.#receive(String(event.data)));
      socket.addEventListener("close", (event) => {
        for (const [id, settle] of this.#pending)
          settle({ type: "primitiveResult", id, queued: false, reason: "The connection closed first." });

        this.#pending.clear();
        resolve(this.#ending(event.code, event.reason));
      });
    });
  }

  /** Waits `ms` milliseconds, or until Ctrl+C. */
  #wait(ms: number): Promise<void> {
    return new Promise((resolve) => {
      const timer = setTimeout(() => this.#stopWaiting?.(), ms);

      this.#stopWaiting = () => {
        clearTimeout(timer);
        this.#stopWaiting = undefined;
        resolve();
      };
    });
  }

  #write(message: MindMessage): void {
    this.#socket?.send(JSON.stringify(message));
  }

  #receive(data: string): void {
    const message = JSON.parse(data) as ServerMessage;

    switch (message.type) {
      case "refused": {
        // The server closes the connection next.
        this.#refusal = { reason: message.reason, retry: message.retry };
        break;
      }
      case "replaced": {
        // The server closes the connection next.
        this.#replaced = true;
        break;
      }
      case "entered": {
        this.#entered = true;
        this.#options.log(`${this.#options.character} is in the world.`);
        this.#handlers.onEntered?.(this);
        break;
      }
      case "percept": {
        this.#handlers.onPercept?.(message, this);
        break;
      }
      case "primitiveResult": {
        this.#pending.get(message.id)?.(message);
        this.#pending.delete(message.id);
        break;
      }
    }
  }

  #ending(code: number, reason: string): Ending {
    if (this.#refusal !== undefined)
      return { kind: "refused", reason: this.#refusal.reason };

    if (this.#replaced)
      return { kind: "replaced" };

    // A normal close after `entered` means the server has disconnected the Agent.
    if (this.#entered && code === 1000)
      return { kind: "left" };

    return { kind: "lost", code, reason };
  }
}

function describe(ending: Ending, character: string): string {
  switch (ending.kind) {
    case "left":
      return `The server has disconnected ${character}.`;
    case "refused":
      return `Refused: ${ending.reason}`;
    case "replaced":
      return `Another Mind has taken over ${character}'s connection, so this one is done.`;
    case "lost":
      return `The connection was lost (code ${ending.code}${ending.reason ? `, ${ending.reason}` : ""}).`;
  }
}
