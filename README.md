# Scoreboard for Valheim

Scoreboard is a BepInEx 5 mod that records per-world multiplayer statistics on the host or dedicated server and displays them to players through an in-game overlay.

## Status

**0.11.0 is published as a GitHub prerelease.** The project builds cleanly and its aggregation/persistence logic is automatically tested. In-game host and dedicated-server acceptance remains pending; see [Manual testing](docs/manual-testing.md).

Built against:
- Valheim Steam build `25185596`
- Unity `6000.0.75f1`
- BepInEx `5.4.23.3`
- HarmonyX `2.9.0`

## Features
- Server-owned storage separated by Valheim world and character ID.
- Connected time, enemy kill credit, boss kill credit, deaths, crafting, building, item pickups, food eaten, portal use, travel, trees, mining, taming, and fishing.
- F8 overlay with themed category views, sorting, and pagination.
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
- **Up/Down:** change statistic category.
- **PageUp/PageDown:** change page.

## Statistic Semantics
- **Time:** server-measured connected time, including AFK time.
- **Kills:** changes to Valheim's `EnemyKills` profile statistic. This is game-defined kill credit, not necessarily the final hit.
- **Bosses:** changes to Valheim's `BossKills` profile statistic.
- **Deaths:** changes to Valheim's `Deaths` profile statistic.
- **Crafts, builds, items, food, portals, distance, trees, mines, tames, and fish:** changes to their corresponding cumulative Valheim profile statistics.
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
