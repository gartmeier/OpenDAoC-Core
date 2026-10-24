import { afterEach, expect, test } from "bun:test";
import type { ServerWebSocket } from "bun";
import { Connection } from "./connection.ts";
import type { MindMessage, PerceptMessage, ServerMessage } from "./protocol.ts";

// Tests call `interrupt()` instead of pressing Ctrl+C, so they leave stdin alone.
// Stands in for the server's Mind endpoint: remembers what the Mind sends and answers as the test says.
function fakeServer(answer: (message: MindMessage, socket: ServerWebSocket<unknown>) => void) {
  const received: MindMessage[] = [];
  const server = Bun.serve({
    port: 0,
    fetch: (request, server) => (server.upgrade(request) ? undefined : new Response("Not a WebSocket", { status: 400 })),
    websocket: {
      message(socket, data) {
        const message = JSON.parse(String(data)) as MindMessage;
        received.push(message);
        answer(message, socket);
      },
    },
  });

  stops.push(() => server.stop(true));
  return { url: `ws://localhost:${server.port}/mind`, received };
}

const stops: (() => void)[] = [];

afterEach(() => {
  for (const stop of stops.splice(0))
    stop();
});

function send(socket: ServerWebSocket<unknown>, message: ServerMessage): void {
  socket.send(JSON.stringify(message));
}

function percept(sequence: number, call: string): ServerMessage {
  return { type: "percept", sequence, elapsedMs: sequence * 10, percept: { kind: "bare", call } };
}

function connect(url: string, handlers: ConstructorParameters<typeof Connection>[1] = {}): Connection {
  return new Connection({ url, token: "secret", character: "Greeen", log: () => {} }, handlers);
}

test("sends a hello first, delivers Percepts in order, and ends as left when the server closes normally after entered", async () => {
  const server = fakeServer((message, socket) => {
    if (message.type !== "hello")
      return;

    send(socket, percept(1, "SendCharacterOverview"));
    send(socket, { type: "entered", tookOver: false });
    send(socket, percept(2, "SendUpdatePoints"));
    socket.close(1000);
  });
  const percepts: PerceptMessage[] = [];
  const connection = connect(server.url, { onPercept: (message) => percepts.push(message) });

  const ending = await connection.run({ handleCtrlC: false });

  expect(server.received[0]).toEqual({ type: "hello", token: "secret", character: "Greeen" });
  expect(percepts.map((message) => message.sequence)).toEqual([1, 2]);
  expect(percepts.map((message) => message.percept)).toEqual([{ kind: "bare", call: "SendCharacterOverview" }, { kind: "bare", call: "SendUpdatePoints" }]);
  expect(connection.entered).toBe(true);
  expect(ending).toEqual({ kind: "left" });
});

test("says whether it entered by taking the Agent over from another Mind", async () => {
  const server = fakeServer((message, socket) => {
    if (message.type !== "hello")
      return;

    send(socket, { type: "entered", tookOver: true });
    socket.close(1000);
  });
  let tookOver: boolean | undefined;
  const connection = connect(server.url, { onEntered: (_, entered) => (tookOver = entered) });

  await connection.run({ handleCtrlC: false });

  expect(tookOver).toBe(true);
});

test("ends as refused, with the server's reason", async () => {
  const server = fakeServer((_, socket) => {
    send(socket, { type: "refused", reason: "No Character is named 'Greeen'.", retry: false });
    socket.close(1000);
  });

  const ending = await connect(server.url).run({ handleCtrlC: false });

  expect(ending).toEqual({ kind: "refused", reason: "No Character is named 'Greeen'." });
});

test("interrupting in the world sends a stop, then a quit, and waits for the server to close the connection", async () => {
  const server = fakeServer((message, socket) => {
    if (message.type === "hello")
      send(socket, { type: "entered", tookOver: false });

    if (message.type === "primitive") {
      send(socket, { type: "primitiveResult", id: message.id, queued: true });

      if (message.primitive.name === "quit")
        socket.close(1000);
    }
  });

  const ending = await connect(server.url, { onEntered: (connection) => connection.interrupt() }).run({ handleCtrlC: false });

  // The server refuses `/quit` while the Character is moving.
  expect(server.received.slice(1)).toEqual([
    { type: "primitive", id: 1, primitive: { name: "stop" } },
    { type: "primitive", id: 2, primitive: { name: "quit" } },
  ]);
  expect(ending).toEqual({ kind: "left" });
});

test("a Primitive's result comes back for its own request", async () => {
  const server = fakeServer((message, socket) => {
    if (message.type === "hello")
      send(socket, { type: "entered", tookOver: false });

    if (message.type === "primitive")
      send(socket, { type: "primitiveResult", id: message.id, queued: false, reason: "Greeen isn't in the world." });
  });
  let result: unknown;
  const connection = connect(server.url, {
    onEntered: async (connection) => {
      result = await connection.send({ name: "quit" });
      // The second Ctrl+C drops the connection, which ends the test.
      connection.interrupt();
      connection.interrupt();
    },
  });

  await connection.run({ handleCtrlC: false });

  expect(result).toEqual({ type: "primitiveResult", id: 1, queued: false, reason: "Greeen isn't in the world." });
});

test("retries while a refusal says to, saying so, and logs in once the server lets it", async () => {
  let hellos = 0;
  const server = fakeServer((message, socket) => {
    if (message.type !== "hello")
      return;

    if (++hellos < 3) {
      send(socket, { type: "refused", reason: "Greeen is linkdead.", retry: true });
      socket.close(1000);
      return;
    }

    send(socket, { type: "entered", tookOver: false });
    socket.close(1000);
  });
  const lines: string[] = [];
  const connection = new Connection({ url: server.url, token: "secret", character: "Greeen", log: (line) => lines.push(line), retryDelayMs: 10 });

  const ending = await connection.run({ handleCtrlC: false });

  expect(hellos).toBe(3);
  expect(lines.filter((line) => line.includes("Retrying"))).toHaveLength(2);
  expect(lines.find((line) => line.includes("Retrying"))).toContain("Greeen is linkdead.");
  expect(ending).toEqual({ kind: "left" });
});

test("doesn't retry a refusal that doesn't say to", async () => {
  const server = fakeServer((_, socket) => {
    send(socket, { type: "refused", reason: "The token is wrong.", retry: false });
    socket.close(1000);
  });

  const ending = await new Connection({ url: server.url, token: "guess", character: "Greeen", log: () => {}, retryDelayMs: 10 }).run({ handleCtrlC: false });

  expect(server.received).toHaveLength(1);
  expect(ending).toEqual({ kind: "refused", reason: "The token is wrong." });
});

test("interrupting while waiting to retry stops retrying", async () => {
  const server = fakeServer((_, socket) => {
    send(socket, { type: "refused", reason: "Greeen is linkdead.", retry: true });
    socket.close(1000);
  });
  const connection = new Connection({
    url: server.url,
    token: "secret",
    character: "Greeen",
    log: (line) => {
      if (line.includes("Retrying"))
        connection.interrupt();
    },
    retryDelayMs: 60_000,
  });

  const ending = await connection.run({ handleCtrlC: false });

  expect(server.received).toHaveLength(1);
  expect(ending).toEqual({ kind: "refused", reason: "Greeen is linkdead." });
});

test("ends as replaced when another Mind takes over, and doesn't reconnect", async () => {
  const server = fakeServer((_, socket) => {
    send(socket, { type: "entered", tookOver: false });
    send(socket, { type: "replaced" });
    socket.close(1000);
  });
  const lines: string[] = [];
  const connection = new Connection({ url: server.url, token: "secret", character: "Greeen", log: (line) => lines.push(line), retryDelayMs: 10 });

  const ending = await connection.run({ handleCtrlC: false });

  expect(server.received).toHaveLength(1);
  expect(ending).toEqual({ kind: "replaced" });
  expect(lines.at(-1)).toContain("Another Mind has taken over Greeen");
});

test("ends as lost when the server drops the connection", async () => {
  const server = fakeServer((_, socket) => {
    send(socket, { type: "entered", tookOver: false });
    socket.close(1011, "Server error");
  });

  const ending = await connect(server.url).run({ handleCtrlC: false });

  expect(ending).toEqual({ kind: "lost", code: 1011, reason: "Server error" });
});
