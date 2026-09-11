# Scoreboard for Valheim

Scoreboard is a BepInEx 5 mod that records per-world multiplayer statistics on the host or dedicated server and displays them to players through an in-game overlay.

## Status

**0.12.0 is published as a GitHub prerelease.** The project builds cleanly and its aggregation/persistence logic is automatically tested. In-game host and dedicated-server acceptance remains pending; see [Manual testing](docs/manual-testing.md).

Built against:
- Valheim Steam build `25253764`
- Unity `6000.0.75f1`
- BepInEx `5.4.23.3`
- HarmonyX `2.9.0`

## Features
- Server-owned storage separated by Valheim world and character ID.
- Connected time, enemy kill credit, boss kill credit, deaths, crafting, building, item pickups, food eaten, portal use, travel, trees, mining, taming, and fishing.
- F8 matrix with player names across the top, all statistics down the side, sorting, and pagination of up to ten players at a time.
- One DLL for clients, listen-server hosts, and dedicated servers.
- Persistent cumulative client journals with idempotent server high-water marks.
- Explicit protocol negotiation, sender binding, rate limits, and bounded persistence.

## Installation
1. Install the Valheim-specific BepInEx 5 distribution on the host/dedicated server and each reporting client.
2. Extract the release archive into `BepInEx/plugins/`. The resulting file should be `BepInEx/plugins/Scoreboard/Scoreboard.dll`.
3. Restart Valheim or the dedicated server.
4. Join the world and press **F8**.

The key is configurable in `BepInEx/config/se.junge.valheim.scoreboard.cfg`.

Clients without Scoreboard may connect, but they cannot open the overlay and do not report kills, boss kills, or deaths. Their connected time is still measured by the server and their row is marked with `*` while online.

## Controls
- **F8:** show or hide Scoreboard.
- **Left/Right:** change the sort column.
- **PageUp/PageDown:** change page.

## Statistic Semantics
- **Online:** current connection state; `*` means no recent client statistic report.
- **Time:** server-measured connected time, including AFK time.
- **Kills / Bosses:** Valheim game-defined kill credit, not necessarily the final hit.
- **Deaths:** Valheim's cumulative death count.
- **Crafts:** crafted item count; upgrades are not included.
- **Builds:** placed build-piece count.
- **Pickups:** picked-up item stacks, not the number of individual items in each stack.
- **Food:** accepted food-eating actions.
- **Portals:** successful portal uses.
- **Distance:** travelled distance in kilometres.
- **Trees:** felled trees.
- **Deposits:** depleted resource deposits, not visits to underground mines.
- **Tames / Fish:** creatures tamed and fish caught.
- Existing character totals from other worlds are not imported.

Client counters are not independently verifiable by an unmodified Valheim server. Scoreboard is intended as a cooperative community summary, not a cheat-resistant competitive ranking. Character IDs are not authenticated account identities.

## Data
Scoreboard writes under `BepInEx/config/Scoreboard/`:
- `world-<world-id>.json`: server totals and stream high-water marks.
- `client-<world-id>-<character-id>.json`: the client's persistent cumulative report journal.
- `.bak`: the previous file version after an atomic replacement.

Do not routinely delete client journals: their stream IDs are part of duplicate prevention. Back up data before manual changes. Unknown or corrupt formats are not silently replaced.

## License
[MIT](LICENSE)
