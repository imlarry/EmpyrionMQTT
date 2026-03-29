# Technical Debt

Actionable items that are known but not yet addressed. Items with full design docs are referenced rather than duplicated.

---

## Infrastructure

### WellKnownPaths — migrate to V2 Application.GetPathFor
`ESB.Messaging/Configuration/WellKnownPaths.cs` builds `EsbInfoFile` by concatenating `AppDomain.CurrentDomain.BaseDirectory` with a literal filename. The V2 `IModApi` surface exposes `Application.GetPathFor(ApplicationPath)` which returns the correct platform-aware path for configuration files without relying on base directory assumptions. `WellKnownPaths.LoadEsbInfo()` (and the construction of `EsbInfoFile`) should be migrated to use this API once the right `ApplicationPath` enum value is confirmed.

### Post-build deployment uses robocopy, not xcopy
`ESB/ESB.csproj` post-build event uses `robocopy` to deploy DLLs and YAML to both mod folders. This replaced `xcopy` after a diagnosed failure mode: when a build runs while the game is open, the target DLLs are memory-mapped (loaded as code sections). xcopy's fallback on a `USER MAPPED FILE` error is to unlink the existing file via `SetDispositionInformationEx FILE_DISPOSITION_POSIX_SEMANTICS`, then attempt to write the new file — which also fails. The result is the file is deleted with no replacement, causing a `TypeLoadException` on the next game launch. robocopy logs a skip error and leaves the existing file intact. The `& exit 0` suffix is required because robocopy returns exit code 1 on success (files copied), which MSBuild would otherwise treat as a build failure.

### EmpyrionNetAPI wrapper DLLs
Five `EmpyrionNetApi*.dll` files ship alongside `ESB.dll` purely as a wrapper layer over the older `ModInterface` API flavor. These are a deployment burden and a versioning risk. The plan is to eliminate this dependency once the V2 `IModApi` surface covers the remaining V1-only operations (see [TopicHandlerIntegration.md](TopicHandlerIntegration.md)). Prerequisite: V1 handler coverage is complete or intentionally dropped.

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
Open question about collapsing `{appId}/Q/...` to a flat `ESB/Q/...` prefix with scope-aware handler registration inside ESB. Four unresolved sub-issues (handler authority registry, multi-response for shared V2 handlers, event source discriminator, playfield multicast addressing). See [ApiConsolidation.md](ApiConsolidation.md) for the full design discussion.

### V1 handler coverage gaps
Several V1 API groups have no handler implementation. Priority order: **Server → Faction → Structure.ListGlobal → Playfield.List** (all read-only). Entity and Message come after Tier 3 testing infrastructure is in place. Full breakdown in [TopicHandlerIntegration.md](TopicHandlerIntegration.md).

### V2 partial coverage
Several V2 interfaces expose only a subset of their methods/properties. Notable gaps: `IStructure.GetDevice<T>`, `IPlayfield.Players`/`Entities`, `IPlayer.DamageEntity`/`Move*`. Full breakdown in [TopicHandlerIntegration.md](TopicHandlerIntegration.md).

### V2 interfaces not started
`IEntity`, `INetwork`, `IPortal`, `ISoundPlayer` have no handler implementation. See [TopicHandlerIntegration.md](TopicHandlerIntegration.md).

---

## EDNA

### No MQTT retry on broker unavailability
EDNA connects to the broker once at startup. If the broker is down when the game starts, EDNA silently stays disconnected for the session — there is no retry loop or reconnect attempt.

### ThreatRadar misses initial playfield state
If the player is already on a playfield when EDNA starts, `ThreatRadar` will not request an initial scan until the next `PlayfieldEntered` event fires. Entities present before EDNA connected are invisible to the radar until a playfield transition occurs.
