# Real 1.127 client captures

What a real 1.127 game client sends the server, from character select through world entry, idling and `/quit`. These are the source of truth for the Harness's login sequence and the fixtures for its packet builders' golden-byte tests (`UT_ClientCaptureFixtures` loads them).

- `login-idle-quit-1.tsv` and `login-idle-quit-2.tsv` are two separate sessions of account `agent1` and its Hibernia Character in account slot 300. In each session the client logs in, enters the world, idles for 2–3 minutes, quits, waits at character select and exits.
- Recorded with `/agent capture agent1` on 2026-10-03 and split per session, otherwise unmodified.
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

1. The quit arrives as command 0xAF with data `00` `&q` `00`. `&q` is the `/quit` command's alias.
2. During the server's 20 s quit delay the Character sits. Position updates carry state flag 0x10 (`SITTING`) at data offset 26.
3. About 20 s after the command, the client sends 0xAC (no handler), then character select `00` and overview `03`. It is back at the character screen with its connection still open.
4. Exiting the client from the character screen sends 0xAC and 0xB8 `01` (neither has a handler), then the connection closes.

## Fields that vary between captures

These hold for every packet:

- **Sequence** (wire bytes 2–3): per-connection counter. It starts at 3 for the first recorded packet and increases by 1 per TCP packet. UDP packets carry 0.
- **Session ID** (wire bytes 4–5): assigned by the server. It is 0 until the first character select is answered.
- **Wire byte 8** (high byte of the code): leftover memory in the client. The server ignores it. Builders can write 0x00.
- **Checksum** (last 2 bytes): follows from everything else.

Parameter is fixed per packet type: 0x0000 for 0x10, 0x9D, 0xA3, 0xBE, 0x1D, 0xAC and 0xB8; 0xFFFE for 0xFC; 0xFFFF for the rest.

Packet data, by data offset:

| Code | Varies | Why |
|------|--------|-----|
| 0x9D Region list | 19 | unknown, client-side |
| 0xA3 Ping | 1, 2, 4–7, 11 | see Liveness |
| 0xA9 Position update | 20–21, 26, 30, 32, 35 | session ID, state flags, heading flags (high byte), action flags, health %. Position and speed floats (0–19, little-endian) would vary too, but the Character stood on its saved spot in both captures |
| 0xBA Heading update | 0–1 | session ID |
| 0xBE Create object request | 0–1 | object ID assigned by the server |
| 0xAF Command | everything | the command text |
| 0xF2 UDP ping | 0 | unknown |
| 0x1D (no handler) | everything | unknown |

Everything else (character select, overview, world init, player init, game open, warmap request, and 0x42, 0x43, 0xAC, 0xB8) is byte-for-byte identical apart from the common fields above. Client filter settings come in two fixed variants, which differ in data offsets 17–19: the first in world entry ends `00e44300`, and the two after it end `00a80401`. Both captures match packet for packet.
