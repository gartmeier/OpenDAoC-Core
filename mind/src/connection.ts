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
  | { kind: "refused"; reason: string }
  /** Anything else: the server was unreachable, stopped, or dropped the connection. */
  | { kind: "lost"; code: number; reason: string };

/**
 * One Agent's connection to the server, which is the Agent's network connection (ADR 0005).
 * It logs the Character in, hands Percepts to a handler and sends Primitives. It knows nothing about what the Mind decides.
 * Ctrl+C quits the Agent through the normal quit path and waits for the server to disconnect it. A second Ctrl+C drops the connection, which leaves the Character linkdead.
 */
export class Connection {
  readonly #options: ConnectionOptions;
  readonly #handlers: ConnectionHandlers;
  readonly #pending = new Map<number, (result: PrimitiveResultMessage) => void>();
  #socket: WebSocket | undefined;
  #nextId = 1;
  #entered = false;
  #quitting = false;
  #refusal: string | undefined;

  constructor(options: ConnectionOptions, handlers: ConnectionHandlers = {}) {
    this.#options = options;
    this.#handlers = handlers;
  }

  /** Whether the Character has entered the world. */
  get entered(): boolean {
    return this.#entered;
  }

  /** Connects, sends the hello, and runs until the connection ends. Handles Ctrl+C while it runs. */
  run(): Promise<Ending> {
    const { url, token, character, log } = this.#options;

    return new Promise((resolve) => {
      const socket = new WebSocket(url);
      const onInterrupt = () => this.interrupt();
      this.#socket = socket;
      process.on("SIGINT", onInterrupt);
      log(`Connecting to ${url} to log ${character} in...`);

      socket.addEventListener("open", () => this.#write({ type: "hello", token, character }));
      socket.addEventListener("message", (event) => this.#receive(String(event.data)));
      socket.addEventListener("close", (event) => {
        process.off("SIGINT", onInterrupt);

        for (const [id, settle] of this.#pending)
          settle({ type: "primitiveResult", id, queued: false, reason: "The connection closed first." });

        this.#pending.clear();
        const ending = this.#ending(event.code, event.reason);
        log(describe(ending, character));
        resolve(ending);
      });
    });
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

  #write(message: MindMessage): void {
    this.#socket?.send(JSON.stringify(message));
  }

  #receive(data: string): void {
    const message = JSON.parse(data) as ServerMessage;

    switch (message.type) {
      case "refused": {
        // The server closes the connection next.
        this.#refusal = message.reason;
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
      return { kind: "refused", reason: this.#refusal };

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
    case "lost":
      return `The connection was lost (code ${ending.code}${ending.reason ? `, ${ending.reason}` : ""}).`;
  }
}
