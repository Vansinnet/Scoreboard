# Engine-Facing Contracts

Baseline: Valheim Steam build `25185596`, verified 2026-09-10. Sanitized assembly evidence is in `docs/evidence/reference-baseline.json`.

| Surface | Exact contract and use | Authority/lifecycle | Status |
|---|---|---|---|
| `PlayerProfile.IncrementStat(PlayerStatType, float, bool)` | Public instance `void`; Harmony prefix/postfix reads `GetStat` before/after and records only positive `Deaths`, `EnemyKills`, and `BossKills` changes. | Local active player profile only; not available as complete server data. | Source-verified; Runtime-pending |
| `Game.GetPlayerProfile()` / `PlayerProfile.GetPlayerID()` | Public instance accessors; character ID keys client journals. | Local client/listen host after player profile creation. Character identity, not authenticated account identity. | Source-verified |
| `ZNet.GetPeers()` / `ZNetPeer.IsReady()` | Public peer list and readiness predicate. | Unity/main thread; peers are connection-scoped and removed on disconnect. | Source-verified |
| `ZNetPeer.m_rpc`, `m_playerID`, `m_playerName` | Public fields. Server ignores payload identity and uses the sending peer's character ID/name. `ZNet.RPC_PlayerID(ZRpc,long)` assigns `m_playerID`. | Server-side connection data; character ID remains client-originated game state, not account authentication. | Source-verified; Runtime-pending |
| `ZNet.GetWorldUID()` | Public instance `long`; server sends the world ID after protocol negotiation. | Available on host/server world instance. | Source-verified |
| `ZNet.IsServer()` / `IsDedicated()` / `GetServerRPC()` | Public role/access methods. | Dedicated server has no local player/UI assumptions. | Source-verified |
| `ZRpc.Register` / `Invoke` / `Unregister` | Own RPC names use `.v1`. Hello uses `(int protocol,long world)`. Reports use fixed `(string stream,long deaths,long kills,long bosses)` arguments. Requests use one `int`; board replies use a bounded `ZPackage`. | Registered per peer, explicitly unregistered on peer/session cleanup. Reports/requests require negotiated connection state. | Source-verified; Runtime-pending |
| `ZPackage` board reply | Server writes world ID, count, then bounded row primitives. Client limits package to 200,000 bytes and count to 500 before accepting rows. | Server-to-client only after negotiation; client verifies sender is its server RPC. | Automated; Runtime-pending |
| `ZNet.OnDestroy()` | Private instance `void`, no parameters. Harmony prefix persists/clears mod session before the singleton is removed. | World/network teardown. | Source-verified; Runtime-pending |
| Unity `MonoBehaviour.OnGUI()` | Immediate-mode overlay; matrix restored in `finally`; no rendering on dedicated server or below minimum dimensions. | Client UI thread only. May execute multiple times per frame. | Source-verified; Runtime-pending |

Plugin metadata uses numeric version `0.1.0` because the installed BepInEx constructor parses the attribute through `System.Version`. Runtime maturity is expressed with the GitHub prerelease flag rather than an invalid prerelease suffix in `BepInPlugin`.

## Server-Only Feasibility
- `Character.OnDeath()` sometimes calls `Game.RPC_RegisterKill(...)` locally and sometimes routes through `Game.RegisterKill(...)`. Network observation alone misses valid kill credits.
- `Player.OnDeath()` sends an `OnDeath` object RPC, but object dispatch depends on local ZDO/ZNetView availability.
- `PlayerProfile.IncrementStat(...)` updates local profile statistics without a general server statistics stream.
- Existing chat/sign paths could display limited server-derived text, but they do not provide complete, fair kill/boss statistics or a key-driven custom overlay.

Conclusion: server-only supports connected time and potentially partial events, not the complete requested scoreboard. Full statistics require the client component.

## Protocol V1
1. Client sends `Hello` with protocol 1.
2. Server verifies the connection exists, records negotiation, and returns protocol plus world ID.
3. Client opens and persists its world/character journal before reporting.
4. Client sends cumulative fixed primitive counters. Server binds one stream ID to the connection, binds identity from `ZNetPeer`, validates bounds, applies independent high-water deltas, and acknowledges in-memory receipt.
5. Client retains the journal and resends it after reconnect/restart. Server high-water marks make retries idempotent.
6. Board requests require negotiation and are rate-limited. Server returns at most 500 of at most 1,000 retained characters.

Client statistics are untrusted. A modified client may fabricate its own counters or create up to eight streams for a character. Limits bound resource use; they do not make the ranking cheat-resistant.

## Persistence V1
- Server schema: world ID; up to 1,000 character records; connected seconds; totals; up to eight client-stream high-water marks per character.
- Client schema: stream GUID and cumulative counters bounded to 1,000,000,000 each.
- Writes use a same-directory temporary file, flush-to-disk, atomic replace, and `.bak` previous version where supported.
- Unknown/corrupt schema fails closed. Transient access failures retry without disabling the unrelated server/client side.
- No migration or reset command exists in protocol/schema 1.

## Source Evidence
Method bodies and metadata were inspected from the installed assemblies with Mono.Cecil `0.10.4.0`. BepInEx XML documentation established BaseUnityPlugin/configuration behavior. HarmonyX `2.9.0` supplies owned unpatching. Unity 6 OnGUI documentation establishes immediate-mode lifecycle. These static findings do not establish live multiplayer behavior.
