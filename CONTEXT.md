# OpenDAoC

A Dark Age of Camelot game server, extended with an experiment in which LLM-driven Agents play the game alongside a single Human.

## Language

### Game

**Account**:
A login identity on the server that owns one or more Characters.

**Character**:
An in-world avatar belonging to an Account, with a realm, race and class.
_Avoid_: toon, avatar

### Agents

**Agent**:
A persistent LLM-driven identity (persona plus memory) that owns exactly one Account and one Character. It exists whether or not it is online; wiping its memory makes a new Agent that inherits the Character.
_Avoid_: bot, mimic, NPC, player

**Human**:
A person playing through the real game client.
_Avoid_: player (when contrasting with Agents)

**Possession**:
A Human logging into an Agent's Account; the Agent is not playing while possessed.

**Harness**:
The part of an Agent that lives inside the server and stands in for the game client, including everything a client does on its own, such as keeping the connection alive.
_Avoid_: body, bot client

**Mind**:
The part of an Agent that lives outside the server and makes its decisions.
_Avoid_: brain

**Primitive**:
One entry in the Harness's fixed vocabulary of things an Agent decides to do, such as moving, attacking or saying something.
_Avoid_: command, action, tool

**Percept**:
One thing the Harness tells the Mind; only what the server would have told a real game client.
_Avoid_: event, observation

**Tactic**:
A unit of Mind-side code that pursues a short-term aim by issuing Primitives, written by a Human or by the Agent itself.
_Avoid_: skill, script, procedure, routine

**Tactic Library**:
An Agent's collection of Tactics.
_Avoid_: skill library

**Plan**:
An Agent's current ordered goals.
_Avoid_: strategy

**Planning**:
The LLM producing or revising a Plan.

**Reflection**:
The LLM reviewing outcomes to revise Tactics and record Lessons.

**Lesson**:
A durable insight recorded by Reflection that informs future Planning.

**Journal**:
An Agent's append-only record of Percepts, Primitives, Planning and Reflection; the material the Human studies.
_Avoid_: event log, telemetry
