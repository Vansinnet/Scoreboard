# Manual Runtime Acceptance

Automated validation does not prove Harmony execution, RPC delivery, UI rendering, multiplayer authority, or persistence during process failure. Record the exact Valheim build, mod version, BepInEx version, machine role, test profile, and observations.

## Listen Server
Use one host and two clients with the same Scoreboard version.

- Confirm the plugin loads without Harmony errors.
- Open and close the F8 overlay; change the configured key.
- Verify sorting, pagination, low resolution, chat focus, inventory, map, and menu input boundaries.
- Verify host and client connected time.
- Compare solo and shared enemy kills against changes to Valheim's `EnemyKills` statistic.
- Compare boss participation against `BossKills`.
- Verify combat, fall, and drowning deaths are counted once.
- Verify name changes preserve a character row and a new character creates a new row.
- Visit two worlds with one character and confirm isolation.
- Disconnect/reconnect and verify no duplicate counters.
- Test a client without Scoreboard: connected time is recorded, client statistics are absent, and the row is marked with `*`.

## Dedicated Server
- Start headless and confirm no player/UI/graphics assumptions or Harmony failures.
- Connect two modded clients and repeat kill, boss, death, reconnect, and world-restart cases.
- Confirm client-reported identity is bound to the connected peer's character ID.
- Confirm an incompatible/unknown protocol cannot report or request board data.
- Confirm repeated report/request traffic is rate-limited and reconnect cleanup removes per-connection state.

## Persistence And Failure
- Stop normally and verify server and client data after restart.
- Interrupt the server after an acknowledged report but before its periodic save; reconnect and verify journal replay restores the update once.
- Restore an older client journal and verify each counter can resume independently without duplicating accepted values.
- Make storage temporarily unavailable and verify retry behavior does not disable unrelated host/server functionality.
- Test a corrupt copy of each schema: Scoreboard must report the error and preserve the original file.
- Verify player and stream limits fail closed without unbounded memory/disk growth.

## Current Status

`0.1.0` is **Runtime-pending**. No in-game host or dedicated-server result has been observed in this workspace. It may only be published as a prerelease until this checklist has documented evidence.
