# OpenDAoC

A Dark Age of Camelot game server, extended with an experiment in which LLM-driven Agents play the game alongside a single Human.

## Language

### Game

**Account**:
A login identity on the server that owns one or more Characters.

**Character**:
An in-world avatar belonging to an Account, with a realm, race and class.
_Avoid_: toon, avatar

**Mob**:
An NPC a Character can fight.
_Avoid_: monster, creature, enemy

**Bindstone**:
A fixed place in the world where a Character can bind, making it the Character's Bind point.

**Bind point**:
Where a Character comes back to life when it releases after dying.
_Avoid_: bindpoint, bind spot

**World object**:
Something in the world that is neither a Character nor an NPC, such as a Grave, a door or loot on the ground.
_Avoid_: item, static item

**Grave**:
A World object left where a Character died; its Character can pray at it to win back some of the experience it lost.
_Avoid_: gravestone, tombstone

**Style**:
A special melee attack a Character learns as one of its weapon specs rises. Many Styles can only be used right after something else happens, such as a parry, an attack from the side or another Style.
_Avoid_: skill, special attack

**Spell**:
Magic a Character casts from one of its Spell lines, such as a bolt, a heal or a buff.
_Avoid_: skill

**Specialization**:
Something a Character trains with points as it levels; raising it brings new Spells or Styles.
_Avoid_: spec, skill line

**Spell line**:
The Spells under one Specialization. A baseline Spell line gives its Spells as the Character levels, and any other gives them as the Specialization rises.
_Avoid_: spell list

**Kiting**:
Fighting a Mob from range: sprinting away from it, turning round and casting at it before it closes in, and again.
_Avoid_: pulling (which only brings a Mob to you)

**Power**:
What a Character spends to cast Spells, refilled by resting.
_Avoid_: mana

**Concentration buff**:
A buff Spell that lasts until its caster cancels it and uses part of the caster's concentration while it's up. It turns off while the Character wearing it is too far from the caster and comes back on when they're close again; it doesn't end.
_Avoid_: conc, const, permanent buff

**Effect**:
A buff or debuff on a Character, shown in its buff bar with its time left.
_Avoid_: buff (when meaning both)

**Dialog**:
A question the server puts to a Character in a window, answered with Accept or Decline, or only acknowledged with OK.
_Avoid_: popup, prompt

**Group**:
Up to eight Characters who have joined together to share the experience from their kills and talk in their own chat channel.
_Avoid_: party

**Navmesh**:
A zone's static map of where a Character can walk or swim, built from the game client's zone data; it says nothing about what changes, such as whether a door is open or where NPCs are.
_Avoid_: pathing grid, nav data

**Swim leg**:
A stretch of a route that a Character crosses swimming on the water's surface, slower than running and never under it.
_Avoid_: water path, dive

### Agents

**Agent**:
A persistent LLM-driven identity (persona plus memory) that owns exactly one Account and one Character. It exists whether or not it is online; wiping its memory makes a new Agent that inherits the Character.
_Avoid_: bot, mimic, NPC, player

**Human**:
A person playing through the real game client.
_Avoid_: player (when contrasting with Agents)

**Possession**:
A Human logging into an Agent's Account; the Agent is not playing while possessed.

**Takeover**:
A new Mind for an Agent that's already online taking over its connection from the old Mind; the Character never leaves the world.

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
One thing the Harness tells the Mind; only what a real game client would know, whether the server told it (another Character moved) or it worked it out itself (its own position while running).
_Avoid_: event, observation

**Self**:
What an Agent currently believes about its own Character, such as where it is, which way it faces, how fast it may run, whether it's on its way somewhere, which Effects are on it, how much Power it has, which Concentration buffs it keeps up and on whom, how much Constitution it has lost and which Dialog is open, built from Percepts. Only that a Dialog was answered comes from the Primitives it sent, as a client closes the window on the click.
_Avoid_: player, status, me

**Surroundings**:
What an Agent currently believes is around it, such as who and what is near and where, built only from Percepts.
_Avoid_: world model, world state

**Inventory**:
What an Agent currently believes it carries, such as what it has equipped, what's in its backpack, its money and how much weight it carries against how much it can, built from Percepts. Only which merchant it's talking to comes from the Primitives it sent, as a client knows what it clicked.
_Avoid_: bag, items

**Tactic**:
A unit of Mind-side code that pursues an aim by issuing Primitives and running other Tactics, and ends having succeeded or failed with a reason, written by a Human or by the Agent itself.
_Avoid_: skill, script, procedure, routine

**Lesson**:
A durable insight the Agent writes down while playing, and reads again before it next acts. It belongs to that one Agent.

**Learning**:
A working pattern or gotcha a Human writes down for every Agent, often promoted from a Lesson of an earlier Agent.
_Avoid_: tip, recipe

**Wiki**:
Game data written out for every Agent, such as where Mobs spawn and what merchants sell; what a Human would look up on a fan site.

**Journal**:
An Agent's record of the Percepts it got, the Primitives it sent and the notes it kept while playing; the material the Human studies.
_Avoid_: event log, telemetry
