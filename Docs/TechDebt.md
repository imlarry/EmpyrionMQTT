# Technical Debt

Actionable items that are known but not yet addressed. Items with full design docs are referenced rather than duplicated.

---

## Infrastructure

### WellKnownPaths — migrate to V2 Application.GetPathFor
`ESB.Messaging/Configuration/WellKnownPaths.cs` builds `EsbInfoFile` by concatenating `AppDomain.CurrentDomain.BaseDirectory` with a literal filename. The V2 `IModApi` surface exposes `Application.GetPathFor(AppFolder)` which returns the correct platform-aware path without relying on base directory assumptions. `WellKnownPaths.LoadEsbInfo()` (and the construction of `EsbInfoFile`) should be migrated to use `AppFolder.Mod` (value 3), confirmed correct for Client instances.

**Open question:** `GetPathFor(AppFolder.Mod)` may return a different or incorrect path on `PlayfieldServer` and `DedicatedServer` instances, where the process working directory and mod deployment layout differ from the Client. Before completing the migration, verify the returned path on all three process types. If `AppFolder.Mod` is wrong for non-Client processes, guard with a conditional on `Application.Mode`:

```csharp
var modPath = _modApi.Application.Mode == ApplicationMode.Client
    ? _modApi.Application.GetPathFor(AppFolder.Mod)
    : AppDomain.CurrentDomain.BaseDirectory;  // fallback until confirmed
```

Note: `WellKnownPaths` is a static class with no `IModApi` reference today. The migration requires either passing the resolved path in at startup (e.g. an `Initialize(string modPath)` call from `ModBase`/`ModApi` entry points) or converting `EsbInfoFile` from a `static readonly` field to a lazily-resolved property.

### EmpyrionNetAPI wrapper DLLs
Five `EmpyrionNetApi*.dll` files ship alongside `ESB.dll` purely as a wrapper layer over the older `ModInterface` API flavor. These are a deployment burden and a versioning risk.

### SP/coop topology with ModTargets
The `ModTargets` field in `ESB_Info.yaml` lets server admins suppress ESB by mode (e.g. `ModTargets: DedicatedServer` silences all PlayfieldServer instances). The correct default and the expected behavior in single-player and local co-op is not yet defined or tested. Observed from live MP test (2026-03-22): N players on a playfield = N `PlayfieldServer` ESB instances publishing duplicate event streams. Needs a decision on whether `ModTargets` default should suppress PlayfieldServer, and whether SP topology (Client-only) needs special handling.

---

## Testing Infrastructure

### Tier 3 testing not operational
Four prerequisites for destructive integration tests are unbuilt. Until these are done, six V1.Player handlers have no test coverage. See [TESTING.md — Deliverable Assignments](TESTING.md#deliverable-assignments) for the full specification of each:

| Assignment | Deliverable |
|---|---|
| 1 | `ESBTests/TestSave/Baseline/` — known-state dedicated server save archive |
| 2 | `Scripts/Restore-TestSave.ps1` — stops server, copies baseline over active save |
| 3 | Item ID catalog — `TestItemId`, `ToolbeltSlot0ItemId`, `BagSlot0ItemId` |
| 4 | `KnownState.Baseline` nested class with all constants filled |

### V1 Server and Message handler tests are partial

Fire-and-forget handlers (`V1.Message.ToPlayer`, `V1.Message.ToAll`, `V1.Message.ToFaction`,
`V1.Server.ConsoleCommand`) are verified as dispatched (`{"Ok":true}`) but message receipt
is not confirmed -- the V1 API provides no delivery acknowledgement.

`V1.Message.ToFaction` test assumes the player is in a faction (`factionId != 0`). It will
fail with an assertion error if the test player has no faction assigned.

`V1.Message.Dialog` requires manual player interaction during the test run (player must click OK).
Automated execution without a live player will time out.

---

### Player.Connected event not tested
`Event_Player_Connected` and `Event_Player_Disconnected` have not fired in any captured session. Both captured sessions had the remote player already connected before ESB started. A test requires a second player connect/disconnect during a live capture with ESB already running.

---

## API Coverage

### Topic collapse / unified API — open design

Current topic format uses `{appId}/Q/...` where `appId` is `Client`, `DedicatedServer`, or `PlayfieldServer`. Proposed collapsed format: `ESB/{msgclass}/{subject}/{clientId}/{seq}` -- the process prefix is removed and ESB routes internally. This would make the topic interface fully mode-agnostic for callers (EDNA skills, Lua scripts).

Recommended sequence if pursued: (1) build scope-aware handler API with V1/V2 routing inside ESB, (2) evaluate topic collapse once handler-level routing is proven.

Four unresolved sub-issues:

**1. Scope-aware handler registration.** With a flat `ESB/Q/...`, all three ESB instances receive every request. A handler registration mechanism must declare which process type(s) are authoritative per subject so non-authoritative instances drop the message silently.

**2. Multi-response for shared V2 handlers.** Some handlers are valid on all process types (e.g. `Application.Mode`, `Application.GameInfo`). With a flat topic all three instances respond to one request. Options: designate a canonical source per handler; accept multiple responses for broadcast-style queries; restrict V2 handlers so only one process type responds.

**3. Event source discriminator.** Today `Client/E/Application.GameEnter` and `DedicatedServer/E/Application.GameEnter` are distinguishable by topic. Collapsed to `ESB/E/Application.GameEnter`, the source process is invisible unless moved into the payload or retained as a partial prefix for events only (`ESB/E/{appId}/{subject}/...`).

**4. Playfield multicast addressing.** The current `PlayfieldServer/Q/+/*/#` pattern fans out to all loaded PlayfieldServer instances simultaneously (broadcast). With a flat prefix, fan-out is implicit but targeted queries to a specific PlayfieldServer instance lose their addressing mechanism. A replacement scheme is needed for targeted vs. broadcast playfield queries.

### V1 handler coverage gaps
Several V1 API groups have no handler implementation. Priority order: **Server → Faction → Structure.ListGlobal → Playfield.List** (all read-only). Entity and Message come after Tier 3 testing infrastructure is in place. Full breakdown in [TopicHandlerCoverage.md](TopicHandlerCoverage.md).

### Request_Entity_Destroy2 -- semantics unknown, not exposed

`CmdId 70 Request_Entity_Destroy2` is present in the API table alongside `CmdId 38 Request_Entity_Destroy`. Both carry the same `Id(entityId)` signature and `Event_Ok` response. The current `V1.Entity.Destroy` handler calls only `Request_Entity_Destroy` (38). `Destroy2` is not exposed.

The distinction between the two variants is undocumented in the Eleon API. Candidates:
- soft vs. hard destroy (e.g. one triggers loot drop, the other removes silently)
- legacy vs. updated destroy path added in a later game version
- one variant may only work on certain `EntityType` values

**Research needed:** spawn a test entity, call `Destroy` vs. `Destroy2`, and observe whether the game behavior differs (loot, recycling, error response, EntityType restrictions). Once semantics are known, either add `V1.Entity.Destroy2` as a separate handler or alias it to `V1.Entity.Destroy` with a documented note.

### V2 partial coverage
Several V2 interfaces expose only a subset of their methods/properties. Notable gaps: `IStructure.GetDevice<T>`, `IPlayfield.Players`/`Entities`, `IPlayer.DamageEntity`/`Move*`. Full breakdown in [TopicHandlerCoverage.md](TopicHandlerCoverage.md).

### V2 interfaces not started
`IEntity`, `INetwork`, `IPortal`, `ISoundPlayer` have no handler implementation. See [TopicHandlerCoverage.md](TopicHandlerCoverage.md).

---

## EDNA

### No MQTT retry on broker unavailability
EDNA connects to the broker once at startup. If the broker is down when the game starts, EDNA silently stays disconnected for the session — there is no retry loop or reconnect attempt.

### ThreatRadar misses initial playfield state
If the player is already on a playfield when EDNA starts, `ThreatRadar` will not request an initial scan until the next `PlayfieldEntered` event fires. Entities present before EDNA connected are invisible to the radar until a playfield transition occurs.
