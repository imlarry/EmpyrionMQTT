# Empyrion Modding API Layers

ESB uses both Empyrion modding API layers simultaneously. Understanding which layer is available where is essential for writing correct handlers and routing MQTT requests.

## The Two Layers

### V1 — ModBase / ModInterface / DediAPI

`EmpyrionModBase` — the older event-driven API accessed via `EmpyrionNetAPIAccess`.

- **`Initialize()` is only called by the game on DedicatedServer in multiplayer.**
- It is **never invoked in SinglePlayer** — ModBase is a complete no-op in SP.
- Provides server-wide async RPC operations that have no V2 equivalent:
  - Player inventory read/write
  - Credits read/write
  - Cross-playfield teleport (of any entity)
  - Player connect/disconnect events
  - Global structure list (including entities on idle/unloaded playfields)
  - Faction graph

### V2 — IModApi (object-oriented)

`IMod` / `IModApi` — the newer object-oriented API.

- **`Init()` is called on every process that loads the mod**: Client, DedicatedServer, and PlayfieldServer.
- Provides object-oriented access to local process state: entities, playfields, player cache, application info.
- **Scope of data varies by process type** — the same call returns different results depending on where it runs:
  - `Application.GetPlayerEntityIds()` on Client = locally visible players only
  - `Application.GetPlayerEntityIds()` on DedicatedServer = all players including offline

## Availability by Process

| Process         | Game Mode    | V1 ModBase   | V2 IModApi             | MQTT appId        |
|-----------------|--------------|--------------|------------------------|-------------------|
| Client          | SP or MP     | Never called | Yes                    | `Client`          |
| DedicatedServer | MP only      | Yes          | Yes (log-confirmed)    | `DedicatedServer` |
| PlayfieldServer | MP only      | Likely No    | Yes (events observed)  | `PlayfieldServer` |

**Notes:**
- SinglePlayer has no DedicatedServer or PlayfieldServer processes — only `Client` exists.
- Both V1 and V2 initialize in the **same process** on DedicatedServer (dual inheritance: `EmpyrionModBase` + `IMod`), sharing a single `ContextData`.
- V2 on PlayfieldServer is confirmed by observing game events published with `PlayfieldServer` as the source appId. Full V2 API coverage beyond events is assumed but not exhaustively verified.

## Scope-Aware Routing Model

The right way to think about which process to target:

| Desired scope | Target | Notes |
|---|---|---|
| Local player data (self) | `Client/Q/...` | V2 cache; works in SP and MP; preferred for EDNA |
| All entities on one playfield | `PlayfieldServer/Q/...` | V2; target by clientId to reach specific PF |
| Server-wide read | `DedicatedServer/Q/...` | V2 on DS has broader scope |
| V1-only operation | `DedicatedServer/Q/...` | V1 only available here |

EDNA skills always target `Client/Q/...` — correct for both SP and MP since EDNA's scope is local-to-player.

## The Read-Through Cache Pattern

Some V1 operations overlap with V2 data that is already cached locally on the Client. Example:

- **V2** `IPlayer.Bag` + `IPlayer.Toolbar` — local cache on Client, instant, covers the **local player only**
- **V1** `Request_Player_GetInventory(entityId)` — server-side RPC, covers **any player by entityId**

For a player asking about their own inventory, V2 is sufficient and preferred — no round-trip needed, works in SP. V1 is only needed for server-side queries about other players. A unified `Player.GetInventory` handler should prefer V2 for self-scoped requests and fall through to V1 for arbitrary entityId queries.

## Implications for Handler Authors

- V1 handlers registered in `SubscriptionHandler` are **silently unreachable in SP and on Client**. Safe to register unconditionally; they simply won't receive requests outside of a multiplayer dedicated server.
- If a V1-backed handler is eventually exposed through a unified API surface, it should return a structured error response (message class `X`) for SP callers rather than timing out silently:
  ```json
  { "Error": "NotAvailableInSinglePlayer", "Method": "Player.GetInventory" }
  ```
- Some V1 operations are inherently multi-player by definition (other players' inventories, cross-PF teleport of another entity). The error for these should distinguish between "not available in SP" and "requires DedicatedServer scope."
