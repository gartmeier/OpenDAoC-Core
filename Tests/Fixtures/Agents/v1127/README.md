# Real 1.127 client captures

What a real 1.127 game client sends the server, from character select through world entry, idling, saying something, moving and `/quit`. These are the source of truth for the Harness's login sequence and Primitives, and the fixtures for its packet builders' golden-byte tests (`UT_ClientCaptureFixtures` loads them).

- `login-idle-quit-1.tsv` and `login-idle-quit-2.tsv` are two separate sessions of account `agent1` and its Hibernia Character in account slot 300. In each session the client logs in, enters the world, idles for 2–3 minutes, quits, waits at character select and exits.
- Recorded with `/agent capture agent1` on 2026-10-03 and split per session, otherwise unmodified.
- `say-hello.tsv` is 17 s of account `admin`'s Hibernia Character standing in the world while the Human types `hello` (plain, no slash), then `/send greeen hello` and `/agent capture off`. Recorded with `/agent capture admin` on 2026-10-20, unmodified. See Say.
- `run-turn-stop.tsv` is 62 s of the same Character on flat open ground in zone 202. It stands, runs in a straight line and stops, strafes right, turns on the spot with the keyboard, runs past an NPC and stops, then targets the NPC. Before each part the Human said `run`, `turn` and `npc`, which mark the parts in the capture as say commands. Recorded with `/agent capture admin` on 2026-10-20, unmodified. See Movement.
- `target-attack-sit.tsv` is 91 s of account `joshua`'s Hibernia Character next to a low-level Mob. It attacks with something targeted, targets the Mob, clears the target, then fights the Mob until it dies, and types `/sit`, `/sit` and `/stand`. Recorded with `/agent capture joshua` on 2026-10-23, unmodified apart from its first line. See Fighting and resting.
- `items-merchants.tsv` is 15 minutes of account `joshua`'s Character Nuschka, from login at Connla: talking to merchants, buying and selling, equipping and unequipping by dragging, examining items, dropping one, trying salvage, picking up loot and typing `/sell all`. Recorded with `/agent capture joshua` on 2026-10-23, unmodified apart from its first line. See Items and merchants.
- `sit-key.tsv` is 26 s of the same Character sitting and standing twice with the sit key. Recorded with `/agent capture joshua` on 2026-10-23, unmodified apart from its first line. See Fighting and resting.
- `styles.tsv` is 209 s of account `agent2`'s Character Dalta, a level 5 Blademaster, already in the world, fighting Mobs and using styles from the quickbar. Recorded with `/agent capture agent2` on 2026-10-24, unmodified apart from its first line. See Styles.
- `healer-dialog.tsv` is 188 s of the same Character, who had lost Constitution, from login: it talks to a Healer and declines the Dialog asking whether to pay to restore it, then talks to the Healer again and accepts. Recorded with `/agent capture agent2` on 2026-10-25, unmodified apart from its first line. See Dialogs.
- `use-item.tsv` is 81 s of the same Character, already in the world at a quiet spot in Hibernia, using its Personal Bind Recall Stone in backpack slot 40: from the backpack, from the quickbar, while moving, once more until it teleports to its Bind point, then three times there on the stone's reuse timer. Recorded with `/agent capture agent2` on 2026-10-25, unmodified apart from its first line. See Using an item.
- `login-next-to-agent.tsv` is 189 s of the same Character next to the Agent Greeen, who is already in the world. The Human says `come` and types `/speed 0` with Greeen targeted, quits to the character screen with `/q`, logs back in next to Greeen, says `come`, types `/speed 191`, says `come` three more times and types `/jump greeen to vue`. The first 189 s of a recording with `/agent capture admin` on 2026-10-21, taken while it was still running, otherwise unmodified. See Players already in the world.
- `swim-straight.tsv`, `swim-turn.tsv` and `swim-stop-sit-stand.tsv` are 43 s, 31 s and 54 s of the same Character, unbuffed, at a river in zone 207. In the first it runs down the bank, swims straight across and runs up the other bank. In the second it swims in, turns about while swimming, and swims back out. In the third it swims in and stops in the water, sits, stands, swims on and runs out. Recorded with `/agent capture agent2` on 2026-10-25, unmodified. See Swimming.
- `cast-heal.tsv` and `cast-buffs.tsv` are 13 s and 19 s of account `druid`'s Character Fearnan, a Naturalist, already in the world at Connla. In the first, at level 1, it casts Minor Revigoration twice. In the second, at level 2, it casts Minor Revigoration and Minor Bark Skin on another and on itself, then cancels two buffs in the concentration window. Recorded with `/agent capture druid` on 2026-10-25, unmodified apart from their first lines. See Spells.
- `cast-damage.tsv` is 640 s of account `agentorla`'s Character Orla, a level 1 Magician, from login in Shannon Estuary: it casts Lesser Powerward on itself, targets a Mob and casts Gleam Ray at it until it dies, then targets itself and quits. Recorded with `/agent capture agentorla` on 2026-10-25, unmodified apart from its first line. See Spells of a list caster.
- `quick-cast.tsv` is 71 s of account `admin`'s Character Vue, a level 50 Mentalist, already in the world: it clicks Quick Cast on the quickbar four times, three of them followed by a spell, with a Mob, nothing or itself targeted. Recorded with `/agent capture admin` on 2026-10-25, unmodified apart from its first line. See Abilities.
- `pet-window.tsv` is 207 s of account `agentfearnan`'s Character Fearnan, a level 5 Druid, from login: it summons its pet, then clicks each button in the pet window once, each announced with a say, casts spells from the quickbar while the pet is out, and releases it. Recorded with `/agent capture agentfearnan` on 2026-10-26, unmodified apart from its first line. See Pets.
- Packets sent before the server knows the Account aren't recorded: the version and crypt-key request (0xF4) and the login request (0xA7). The Harness skips both.

## Format

One packet per line, tab-separated: elapsed ms since capture start, transport, client state when the packet arrived, packet code, then the whole packet in hex as it arrived on the wire. Lines starting with `#` are comments. `CapturedPacket` reads and writes it.

Wire layout: data size (2 bytes), sequence (2), session ID (2), parameter (2), code (2; the server only reads the low byte), data, checksum (2). All big-endian. Data offsets below count from the start of the data, i.e. wire offset − 10.

## World entry

Identical in both sessions, except that player init and the ping after it swap places. The client sends it in bursts: character select, then about 20 s at the character screen while the Human picks the Character, then everything else within about 2 s.

| # | Code | Packet | Data | Server reads |
|---|------|--------|------|--------------|
| 1 | 0x10 | Character select | `00` | nothing; replies with login granted and the session ID |
| 2 | 0xFC | Character overview | `03` (realm: Hibernia) | the realm |
| 3 | 0x9D | Region list | 29 bytes, starting `14` (selected slot) | the slot byte |
| 4 | 0xA3 | Ping | see Liveness | |
| 5 | 0x10 | Character select | `14` (selected slot) | nothing |
| 6 | 0xD4 | World init | `14` (character index) | the index: `realm * 10 - 10 + slot within realm`, so 0x14 is slot 300 |
| 7, 8 | 0xE8, 0xA3 | Player init, ping | `03` | nothing |
| 9 | 0xA9 | Position update | the saved position | the position |
| 10 | 0xBF | Game open | `00` | the flag; the client is now Playing |
| 11 | 0xDC | Client filter settings | 20 bytes, ending `00e44300` | |
| 12 | 0xBE | Create object request | object ID (little-endian) and `0000` | the object ID; a reaction to objects the server sent |
| 13 | 0xAF | Command | `00` `&gc info 1` `00` | the command; sent automatically by the client |
| 14, 15 | 0x1D, 0x1D | (no handler) | 4 bytes | dropped |
| 16, 17 | 0xDC, 0xDC | Client filter settings | 20 bytes, ending `00a80401` | |
| 18 | 0x1D | (no handler) | 4 bytes | dropped |
| 19, 20 | 0x42, 0x43 | (no handler) | `00` | dropped |
| 21 | 0x48 | Show warmap request | `650000bf` | |
| 22 | 0xA9 | Position update | now with full health | the position |

Before step 1, while the client is still Connecting, both sessions send a ping, character select `00` and overview `00` (realm unknown).

## Liveness while idle

- **Ping (0xA3, TCP) every 5000 ms.** Data: bytes 1–2 follow a fixed 11-ping cycle from world entry onwards (`01 1A`, `02 00`, `03 4B`, `04 4B`, `05 4B`, `06 00`, `07 00`, `08 00`, then `00 00` three times), bytes 4–7 are the client's millisecond tick counter, and byte 11 is 1 only on the first ping during world entry. The server reads only bytes 4–7 and echoes them, with the packet's sequence, in its ping reply.
- **Position update (0xA9, TCP) about every 2130 ms, even when standing still.** The server soft link-deads a player whose last position update is more than 5000 ms old (`PlayerMovementComponent.SOFT_LINK_DEATH_THRESHOLD`), so pings alone don't keep a Character from going linkdead.
- **UDP init (0x14) followed by UDP ping (0xF2), over UDP, every 65 s.** The server falls back to sending over TCP when these stop, so a Harness without UDP can skip them.
- The sessions also contain a few heading updates (0xBA) and create object requests (0xBE), reacting to what the server sent.

## Quit

1. The quit arrives as command 0xAF with data `00` `&q` `00`, because the Human typed `/q`, the `/quit` command's alias. Typing `/quit` presumably sends `&quit` in the same layout; that wasn't captured.
2. During the server's 20 s quit delay the Character sits. Position updates carry state flag 0x10 (`SITTING`) at data offset 26.
3. About 20 s after the command, the client sends 0xAC (no handler), then character select `00` and overview `03`. It is back at the character screen with its connection still open.
4. Exiting the client from the character screen sends 0xAC and 0xB8 `01` (neither has a handler), then the connection closes.

## Say

A chat line typed without a slash goes out as the say command's `/s` alias: command 0xAF, parameter 0xFFFF, data `00` `&s hello` `00`. That's the same layout as every other command: a leading 0, the command with `&` in place of `/`, a terminating 0. The server's `/say` handler (`&say`, alias `&s`) reads the text after the command. The other commands in the capture follow the same layout: `&send greeen hello` and `&agent capture off`.

Full packet as captured: `000A 010C 0001 FFFF 38AF 00 26 73 20 68 65 6C 6C 6F 00 A414`. Only the common fields vary (see below). Wire byte 8 is 0x38 here, more leftover client memory. Typing `/say hello` or `/s hello` presumably sends `&say hello` or `&s hello` in the same layout, as `/q` sent `&q`; neither was captured.

## Movement

From `run-turn-stop.tsv`. Everything happens on flat ground, so the Z coordinate stays 4848 and the Z speed stays 0 throughout. Running uphill or downhill wasn't captured.

### Position update (0xA9) layout

As captured, by data offset:

| Offset | Field | In this capture |
|--------|-------|-----------------|
| 0–11 | X, Y, Z, floats, little-endian, global coordinates | |
| 12–15 | speed, float, little-endian | 0 standing or strafing, up to 191 running |
| 16–19 | Z speed, float, little-endian | always 0 |
| 20–21 | session ID | |
| 22–23 | the target's object ID | 0 until the Human targets the NPC, then its ID 0x1F0B |
| 24–25 | zone ID | 202 |
| 26 | state flags | 0, or strafe flags while strafing (see below) |
| 27–29 | | always 0 |
| 30–31 | heading: low 12 bits, 4096 to a full turn; bit 0x1000 is a flag | flag set in every packet |
| 32 | action flags | 0x80 (`TORCH`), 0xB0 (`TORCH`, `TARGET_IN_VIEW`) once the NPC is targeted |
| 33–34 | | always 0 |
| 35 | health % | 100 |
| 36–39 | | always 0 |

Heading `h` points along (−sin θ, cos θ) with θ = `h` × 2π / 4096, so heading 0 points towards +Y. The first run went (−29.3, −28.3) per update at heading 0x5F3 (1523, 134°). The 0x1000 flag is set in every position update here, and in the login captures in every one except the first after game open (`09DF` against `19DF`). Heading updates never set it. The server keeps it in `RawHeading` and masks it off in `Heading`.

### Running

- **Starting.** The first update goes out as soon as the Character moves, 633 ms after the previous idle update in both runs, not on the idle cadence. It already carries a speed (19.6 and 11.8) and a position 1–1.5 units on. Over the next three updates the speed ramps up (72.6, 127.2, 180.9), then stays at 191, the Character's maximum speed (`GamePlayer.PLAYER_BASE_SPEED`). That's about 0.9 s from standing to full speed.
- **Rate.** One position update every 200 or 233 ms while running (173–260 ms over both runs; mean 212 ms over the first run's 23 intervals), in place of the idle 2130 ms. At 191 that's about 40 units per update, i.e. 191 units per second.
- **What changes.** Only X and Y, the speed, and the heading if the Human steers. Z, Z speed, state flags, the heading's 0x1000 flag, action flags and health stay as they were standing. A straight run keeps its heading (`15F3` throughout the first run). Steering while running, as in the second run, changes the heading in the position updates; no heading update is sent.
- **Stopping.** After the key is released: one update with the speed falling (103.5 and 94.1), about 31 units on, then about 200 ms later one with speed 0 at the final spot, 8–10 units further on. The next update comes about 2130 ms after that, on the idle cadence again. After the second run, a heading update with the final heading followed the speed-0 update after 200 ms.

Full packet at full speed in the first run: `0028 0042 0001 FFFF 00A9 8EC59048 D4DD1C49 00809745 00003F43 00000000 0001 0000 00CA 00 000000 15F3 80 0000 64 00000000 8218`: X 296492.4375, Y 642525.25, Z 4848, speed 191, Z speed 0, session ID 1, no target, zone 202, heading 0x15F3, `TORCH`, health 100.

### Strafing

The Human strafed right before turning. Strafing has its own state flags and no speed: state flags 0xA0 (`STRAFING_RIGHT`, `STRAFING_FULL_SPEED`), speed 0 and the heading unchanged. The Character moves at right angles to its heading, at heading + 1024, about 23 units per update (about 110 units per second). The rate is the running one. The last moving update has only `STRAFING_RIGHT` (0x80), then the state flags go back to 0.

### Turning on the spot: heading update (0xBA)

While the Character turns without moving, the client sends a heading update every 200 or 233 ms (twice 433 ms), each with the heading reached so far. The position updates keep their idle 2130 ms cadence and carry the current heading too. On the keyboard the Human turned about 1.3 full turns in 8.4 s. When turning stops, the heading updates stop. The last one holds the final heading, and the next position update carries it with its 0x1000 flag (`0C1A`, then `1C1A`).

Layout, 14 bytes of data, the same in all four captures:

| Offset | Field | In every capture |
|--------|-------|------------------|
| 0–1 | session ID | |
| 2–3 | | 0. The server reads a target here; no heading update was sent while something was targeted |
| 4–5 | heading, low 12 bits only | never with the 0x1000 flag |
| 6 | | 0 |
| 7 | action flags | 0x80 (`TORCH`) |
| 8 | | 0. The server reads a steed seat |
| 9 | | 0. The server reads the state flags, shifted right by 2 |
| 10 | presumably health %, as in the position update | 100 |
| 11–13 | | 0 |

Full packet, the first of the turn: `000E 0068 0001 FFFF 00BA 0001 0000 06D6 00 80 00 00 64 000000 C5CD`.

### Running past an NPC

While running past the NPC, the client sends a create object request (0xBE) for an object the server sent, as when idling. After stopping, the Human targeted the NPC: target (0xB0) with data `1F0B E000`, the NPC's object ID and two bytes the server reads as flags. From then on, position updates carry 0x1F0B at data offsets 22–23 and action flags 0xB0.

### Swimming

From the three swim captures. The river's surface is at 4690 (the zone's water height in OpenDAoC-BuildNav's `zone207.gset`, 146.5625 × 32), where the navmesh's water sheet sits 16 below it.

- **Going in.** The update in which Z reaches the water already has state flag 0x04 (`SWIMMING`) and the swim speed, with no ramp down: 191 on the bank, then 85. From then on Z stays at 4650, 40 below the surface, with Z speed 0.
- **Speed.** The speed field is 85 while swimming, and the Character covers about 85 units a second. That's 85/191 of its running speed. In an earlier, unchecked-in capture with a speed buff, the same Character ran at 280 and swam at 124, the same fraction, so swim speed follows the maximum speed.
- **What changes.** As when running: X and Y, the speed and the heading. Turning while swimming changes the heading in the position updates only; no heading update is sent. The rate is the running one.
- **Stopping.** The speed falls (81, 9.1), then an update with speed 0 at the final spot, and the idle cadence. State flag 0x04 stays set while standing in the water, in the idle updates too. Starting again ramps the speed up to 85 over about 0.6 s.
- **Sitting.** `/sit` in the water works: the state flags become 0x10 (`SITTING`) with 0x04 cleared, at the same Z. Standing up sets 0x04 again.
- **Coming out.** The first update with Z above 4650 clears 0x04, and the speed ramps up to the running speed over about 0.4 s, as when starting a run.
- Action flags, the heading's 0x1000 flag and health are as on land. The diving action flag (0x02) is never set.

## Players already in the world

The server sends a player's create to those around only when that player enters the world (`GamePlayer.AddToWorld`). A client that enters after it learns of the player from the player's position updates, and asks for its create: create player request (0xD5), parameter 0x0000, data `0400 0000` for Greeen, whose session ID was 4. That's the session ID low byte first, as in the create object request (0xBE), then two bytes of 0. `PlayerCreationRequestHandler` reads the session ID and answers with the player's create. In `login-next-to-agent.tsv` the client sent it right after game open, with the rest of world entry's burst, having had Greeen's position updates during world entry.

## Fighting and resting

From `target-attack-sit.tsv`, and the targeting at the end of `run-turn-stop.tsv`.

- **Target (0xB0).** Data: the object ID, then 2 bytes of flags.
  - A click on a Mob sends `E000`: examine (0x8000), which makes the server describe it, and both line-of-sight bits (0x6000).
  - Esc sends object ID 0 with flags `0000`.
  - When the Mob it was attacking died, the client cleared its target by itself: object ID 0 with flags `0001`, the attack mode bit.
- **Attack request (0x74).** 4 bytes of data: whether to start, then 0 when the Human pressed the attack key or 1 when the client stopped attacking by itself, then 2 bytes. Those were 0 every time the Human pressed the key, and leftover memory when the client stopped by itself, at the same moment as it cleared its target after the Mob died. The server reads only the first two.
- **Position and heading updates while targeting and attacking.** They carry the target's object ID (position update offsets 22–23, heading update offsets 2–3) and `TARGET_IN_VIEW` in the action flags. While attacking, the health byte (position update offset 35, heading update offset 10) has its top bit set: `E4` is 100% while attacking. The server reads the action flags from every update, so a client that left `TARGET_IN_VIEW` out would take its target out of view. It skips the target and the health byte.
- **Sitting.** `/sit` and `/stand` go out as commands (`&sit`, `&stand`), and the server's handlers sit or stand whatever the Character was doing. The sit key is a toggle: the client sends the sit request (0xC7, parameter 0x0000) with data `01 5E 88 04` to sit or `00 5E 88 04` to stand, deciding which from what it believes its state is. The server reads only the first byte. The Harness uses the commands because they say what they mean. While sitting, position updates carry state flag 0x10 (`SITTING`).
- The Human moved on sloped ground while fighting, so some updates carry a Z speed (offsets 16–19). The Harness leaves it at 0.

## Items and merchants

From `items-merchants.tsv`. Every one of these packets has parameter 0x0000. Where a handler reads a field it doesn't use, the client writes its session ID, or its X and Y in whole units. No markers were said: the parts are told apart by their packets.

- **Move item (0xDD).** 8 bytes: the session ID, the destination slot, the source slot and the count, which was 0 every time. Dragging onto a slot sends that slot (gloves to 22, the torso to 25, to and from the backpack). Dragging onto the figure sends 600 (`NewPlayerPaperDoll`), and the server then picks the slot from the item. Dragging an item out of the window sends slot 1, the ground, and the server drops it there.
- **No destroy.** The 1.127 client has no way to destroy an item, so the destroy request (0x80) never appears. Its salvage needs a crafting skill and goes as a detail request of kind 102.
- **Detail request (0xD8).** 12 bytes: the kind of thing (1 an item in the backpack or equipment, 4 an item in a merchant's window, 102 salvage), then `1A000000` for an item and 0 for salvage, then the slot or the merchant window's index (the page times 30 plus the place on the page), then 4 bytes of 0. The server doesn't read the second field for items.
- **Interact request (0x7A).** The client targets the NPC first (0xB0, `E000` as for any click), then sends 12 bytes: X, Y, the session ID and the NPC's object ID. A merchant answers with its window.
- **Buy request (0x78).** 16 bytes: X, Y, the session ID, the index, the count (1), the window type (0, a merchant's normal window) and 2 bytes of 0. The merchant is the client's target.
- **Sell request (0x79).** 12 bytes: X, Y, the session ID and the backpack slot. The merchant is the client's target.
- **Pick up request (0xB5).** The client targets the loot first, then sends a single byte, 0: the server picks up its target.
- `/sell all` sells the whole backpack to the targeted merchant, a server command sent as any other (`&sell all`).

## Styles

From `styles.tsv`. The Human, possessing Dalta, fought Mobs and clicked styles on the quickbar, ending with Return Blade after a parry and then Shining Blade.

- **Examining a style sends nothing.** In two later captures, not checked in, the Human hovered and examined styles, and the client sent only position updates and pings. It shows the tooltip the server sent alongside the skill list (the forced tooltip update of 1.110+ clients).

- **Use skill (0xBB).** Parameter 0x0000, 24 bytes: X, Y and Z as little-endian floats, the speed as a float (0 every time), the heading (the low 12 bits only), 2 bytes of flags, the index, the type and 2 bytes of 0.
  - The flags were `A000` every time, with a Mob targeted: both target-in-view bits, which the server takes from this packet as from a position update.
  - The type was `01`, so the index counts from the first entry after the specializations: Shining Blade was 9 and Return Blade 10.
  - The position is where the client stands, which the server takes as a position update too.

## Dialogs

From `healer-dialog.tsv`. No markers were said: the parts are told apart by their packets.

- **Talking to the Healer** is an interact request (0x7A), after targeting it, as with a merchant. The Healer answers with a custom Dialog.
- **Dialog response (0x82).** Parameter 0xFFFF, 8 bytes: data1, data2 and data3 as the server's Dialog sent them, then the dialog code and the response.
  - For the Healer's custom Dialog: data1 `0003` (the session ID), data2 `0001` (the custom marker), data3 `0000`, code `06` (`CustomDialog`).
  - The response was `00` when declining and `01` when accepting.

## Using an item

From `use-item.tsv`. The Human, possessing Dalta with nothing targeted, used the Personal Bind Recall Stone in backpack slot 40. No markers were said: the parts are told apart by timing and position.

| ms | Part | Speed | Flags | Type |
|----|------|-------|-------|------|
| 38363 | right-click in the backpack; the Character moved 0.7 s later, and no teleport followed | 0 | `0000` | 1 |
| 47997 | click on the quickbar; the Character moved 1.8 s later, and no teleport followed | 0 | `0000` | 0 |
| 56568 | quickbar, while moving | 85 | `4000` | 0 |
| 62097 | quickbar, standing; 10.8 s later the position updates jump to the Bind point at X:338704 Y:444627 | 0 | `0000` | 0 |
| 74530, 75697, 76730 | quickbar, at the Bind point, on the reuse timer | 0 | `0000` | 0 |

- **Use slot (0x71).** Parameter 0x0000, 24 bytes, laid out as the use skill packet (0xBB): X, Y and Z as little-endian floats, the speed as a float, the heading (the low 12 bits only), 2 bytes of flags, the slot, the type and 2 bytes of 0.
  - The slot is the inventory slot itself, 40 for the first backpack slot, from the backpack and from the quickbar alike.
  - The type is 1 from the backpack and 0 from the quickbar. The server's use of a magical backpack item treats 0 and 1 alike, and 2 (an item's second spell) not at all.
  - The flags had no target-in-view bits, as nothing was targeted. While moving they had 0x4000, which the server reads as strafing.
  - The position and speed are the client's, which the server takes as a position update too.
- The server's replies aren't in an inbound capture.

Full packet, the use from the backpack: `0018 0045 0001 0000 0071 60EFA648 14911049 8F0EAB45 00000000 0C00 0000 28 01 0000 5264`.

## Spells

From `cast-heal.tsv` and `cast-buffs.tsv`. The Human played Fearnan, a Naturalist (object ID 0x4549), standing in one spot, and clicked spells on the quickbar. No markers were said: the parts are told apart by their packets.

| Capture | ms | Part |
|---------|----|------|
| `cast-heal.tsv`, level 1 | 2960, 8027 | Minor Revigoration, with Fearnan already targeting itself |
| `cast-buffs.tsv`, level 2 | 3931, 5664 | targets itself (0x4549), then another object (0x2CCE) |
| | 6165, 8264 | Minor Revigoration, then Minor Bark Skin, on 0x2CCE. A line of sight check response (0xD0) follows the first |
| | 11464, 11831, 13698 | targets itself, then the same two spells on itself |
| | 18064, 18631 | cancels two buffs in the concentration window |

- **A hybrid's spell is a skill (0xBB).** It's sent exactly as a style is: the same 24 bytes, flags `A000` with something targeted, type `01`, and the index counting from the first entry after the specializations. Minor Revigoration was 6 and Minor Bark Skin 7. No 0xD5 was sent: that's for list casters.
- **Line of sight check response (0xD0).** Parameter 0xFFFF, 8 bytes: the caster's object ID, the target's, then 4 bytes. It answers the server's check before a spell on someone else lands. The Harness doesn't answer it.
- **Remove concentration effect (0x76).** Parameter 0x0000, 4 bytes: the index in the concentration list, then `00 72 04`, the same both times, which `RemoveConcentrationEffectHandler` doesn't read. Both clicks were index 0: once the first buff was gone, the second moved up.

Full packet, the first cancel: `0004 01F0 0005 0000 B076 00 00 72 04 7775`.

## Spells of a list caster

From `cast-damage.tsv`. The Human played Orla, a Magician (object ID 0x4548), and clicked spells on the quickbar. A Magician's spells aren't in its skill list but in its spell lines: Way of the Sun first, then Way of the Moon.

| ms | Part |
|----|------|
| 29830 | Lesser Powerward, with nothing targeted |
| 47197 | targets the Mob (0x2DB9) |
| 48297 | Gleam Ray, while still running |
| 48997–59006 | Gleam Ray 13 more times, standing, some clicked while the last was still casting. A line of sight check response (0xD0) follows about every 200 ms from 49230 to the end of the fight |
| 60964, 65230 | attack on, then off, and the target cleared |
| 126230, 133897 | targets itself, then `/q` |

- **A list caster's spell is the use spell packet (0x7D).** Parameter 0x0000, 24 bytes: laid out as the skill packet (0xBB), with the spell's level and its line's index in place of the index and its type. Lesser Powerward was level 1 in line 1, with flags `0000` as nothing was targeted. Gleam Ray was level 1 in line 0, with flags `A000`. The first Gleam Ray carried the speed the client was running at, 78.1.
- **Line of sight check response (0xD0).** As for a hybrid's spell, but sent again and again through a cast at a Mob, as the server checks during the cast.

Full packet, the second Gleam Ray: `0018 0084 0005 0000 707D 424D8F48 D7FF1C49 7CE59745 00000000 0065 A000 01 00 0000 50C0`.

## Abilities

From `quick-cast.tsv`. The Human played Vue, a Mentalist, and clicked Quick Cast on the quickbar, then mostly a spell.

| ms | Part |
|----|------|
| 40599 | targets a Mob (0x3B73) |
| 44265, 45165 | Quick Cast, then a level 50 spell of line 0 |
| 47432 | attack on, and the target cleared |
| 48065, 48965 | Quick Cast, then the same spell, with nothing targeted |
| 55232–60699 | targets a Mob (0x0704), Quick Cast, then a level 1 spell of line 0 three times, the last two after moving |
| 62666 | attack on, and the target cleared |
| 64298, 66032 | targets itself (0x4637), then Quick Cast |

- **An ability is a skill (0xBB).** It's sent exactly as a style or a hybrid's spell is: the same 24 bytes, flags `A000` with something targeted and `0000` without, type `01`, and the index counting from the first entry after the specializations. Quick Cast was 3 every time.

## Pets

From `pet-window.tsv`. The Human played Fearnan, a Druid, and said the name of each button in the pet window before clicking it.

| ms | Part |
|----|------|
| 40130 | summons the pet (skill 15) |
| 73330–88263 | aggressive, defensive, passive |
| 96330–116430 | stay, go to target, here, follow |
| 127496, 132930 | attack, before and after saying it |
| 143130–186163 | casts spells from the quickbar while the pet is out |
| 203663, 205803 | release |

- **A click is one byte on its row (0x8A).** Each sends 4 bytes: aggression (1 aggressive, 2 defensive, 3 passive), walk (1 follow, 2 stay, 3 go to target, 4 here) and command (1 attack, 2 release), with 0 on the two rows not clicked, then a 0 that `PetWindowHandler` doesn't read. Parameter 0x0000. Nothing else varies.

## Fields that vary between captures

These hold for every packet:

- **Sequence** (wire bytes 2–3): per-connection counter. It starts at 3 for the first recorded packet of a login (`say-hello.tsv` and `run-turn-stop.tsv` start mid-session, at 0x0104 and 0x0030) and increases by 1 per TCP packet. UDP packets carry 0.
- **Session ID** (wire bytes 4–5): assigned by the server. It is 0 until the first character select is answered.
- **Wire byte 8** (high byte of the code): leftover memory in the client. The server ignores it. Builders can write 0x00.
- **Checksum** (last 2 bytes): follows from everything else.

Parameter is fixed per packet type: 0x0000 for 0x10, 0x9D, 0xA3, 0xBE, 0xC7, 0xD5, 0x1D, 0xAC, 0xB8, 0xDD, 0xD8, 0x7A, 0x78, 0x79, 0xB5, 0xBB, 0x71, 0x76, 0x7D and 0x8A; 0xFFFE for 0xFC; 0xFFFF for the rest.

Packet data, by data offset:

| Code | Varies | Why |
|------|--------|-----|
| 0x9D Region list | 19 | unknown, client-side |
| 0xA3 Ping | 1, 2, 4–7, 11 | see Liveness |
| 0xA9 Position update | 0–15, 20–26, 30–32, 35 | position and speed, session ID, the target's object ID, zone ID, state flags (sitting, strafing), heading and its 0x1000 flag, action flags, health %. See Movement. Z speed (16–19) was 0 throughout, on flat ground in every capture |
| 0xBA Heading update | 0–1, 4–5 | session ID, heading |
| 0xB0 Target | 0–1 | the target's object ID. Only one, in `run-turn-stop.tsv` |
| 0xBE Create object request | 0–1 | object ID assigned by the server |
| 0xAF Command | everything | the command text |
| 0xF2 UDP ping | 0–2 | unknown. Only offset 0 varies between the two login sessions |
| 0x1D (no handler) | everything | unknown |

Everything else (character select, overview, world init, player init, game open, warmap request, and 0x42, 0x43, 0xAC, 0xB8) is byte-for-byte identical apart from the common fields above. Client filter settings come in two fixed variants, which differ in data offsets 17–19: the first in world entry ends `00e44300`, and the two after it end `00a80401`. Both captures match packet for packet.
