# EntityHandler -- Implementation Plan

## Status

Implemented in commit 4e45406 ("Add EntityHandler; move DamageEntity off
PlayerHandler"). Sections 1-6 below are complete. Section 7 (verification) is
user-driven and still open: build/test are user-triggered, and the live
smoke-check has not been run.

Note on threading: the handler methods now hop through
`_ctx.MainThreadRunner.RunOnMainThread` for the entity lookup and the ModApi
call, per the project rule (ModApi/game-state on the main thread, Unity thread
affinity). Payload parse/validation stays off the hop; List reads scalar rows
in the hop and builds the Tabular/ToString off it. Done as the first handler in
the ESB main-thread pass (see Docs/MainThreadAudit.md).

## Context

EmpyrionMQTT exposes the Empyrion modding API over MQTT via topic handlers under
[../ESB/TopicHandlers/](../ESB/TopicHandlers/). Today there are AppHandler,
PlayerHandler, StructureHandler, and PlayfieldHandler. `IEntity` -- the V2 base
interface for any player or BA/CV/SV/HV/animal/item on a playfield, documented in
[Analysis/ApiTableOfContents.md:311-340](Analysis/ApiTableOfContents.md) -- is
currently unreachable as a first-class scope.

The motivating case: trying to "move a structure" only to discover that `Move`,
`Position`, `Rotation`, and `DamageEntity` are all on `IEntity`, not on
`IStructure`. Today callers must reach those members indirectly (and
inconsistently: `Player/DamageEntity` exists but takes no EntityId and silently
damages LocalPlayer).

**Design decision -- pure layering.** EntityHandler becomes the canonical home
for every IEntity-level operation, keyed by `EntityId`. PlayerHandler keeps only
player-specific operations (Teleport, Properties with player-only fields).
StructureHandler keeps only structure-specific operations (Tanks/Blocks/Devices).
Because the project is pre-alpha with no deployed callers, breaking
`Player/DamageEntity` is free; it is removed and re-homed under
`Entity/DamageEntity` with proper EntityId targeting.

## Scope of EntityHandler

Read ops:
- `Entity/GetProperties` `{ EntityId }` -- all safe IEntity getters plus
  `HasStructure` so callers know whether to switch to `Structure/*` ops.
- `Entity/List` (no payload) -- tabular enumeration of every entity on the
  current playfield, richer than
  [Playfield/GetEntities](../ESB/TopicHandlers/PlayfieldHandler.cs) (which
  exists today but only returns Id/Name/Type/Position/IsProxy). Entity/List adds
  FactionId, FactionGroup, BelongsTo, DockedTo, IsPoi.

Write ops:
- `Entity/SetPosition` `{ EntityId, Pos: {X,Y,Z} }`
- `Entity/SetRotation` `{ EntityId, Rot: {X,Y,Z,W} }`

Method ops:
- `Entity/DamageEntity` `{ EntityId, DamageAmount, DamageType }`
- `Entity/Move` `{ EntityId, Direction: {X,Y,Z} }`
- `Entity/MoveForward` `{ EntityId, Speed }`
- `Entity/MoveStop` `{ EntityId }`

Skipped on purpose: `IsLocal`/`IsProxy` (process metadata, surfaced in
Properties only for diagnostics), `Forward` (derivable from Rotation but cheap
so kept in Properties), `LoadFromDSL` (low-level reload, unclear semantics),
`Structure` live reference (replaced by `HasStructure` bool; callers use
Structure scope with the same EntityId).

---

## Checklist

### 1. Shared helpers

- [x] Add `internal static Quaternion ParseQuat(JToken t)` to
      [../ESB/Helpers/MessageHelpers.cs](../ESB/Helpers/MessageHelpers.cs)
      immediately after `ParseVecInt3`. Expects `{X,Y,Z,W}`; symmetric to the
      existing `Vec(Quaternion)` emitter. Use direct cast `(float)t["X"]` per
      the project JToken rule -- no `.Value<float>()`.

- [x] Move the FactionData serializer out of PlayerHandler so it can be reused.
      Today it lives as `static JObject FD(FactionData fd)` at
      [../ESB/TopicHandlers/PlayerHandler.cs:19](../ESB/TopicHandlers/PlayerHandler.cs).
      Promote it to `internal static JObject FactionDataJson(FactionData)` in
      [../ESB/TopicHandlers/HandlerHelper.cs](../ESB/TopicHandlers/HandlerHelper.cs)
      and update both PlayerHandler call sites (the `FactionData` and `Faction`
      property writes) and the new EntityHandler to use it.

### 2. Payload DTOs

Create `ESB/Payloads/EntityPayloads.cs` following the
`<Operation><Request|Response>` naming convention used in
[../ESB/Payloads/StructurePayloads.cs](../ESB/Payloads/StructurePayloads.cs)
and [../ESB/Payloads/PlayerPayloads.cs](../ESB/Payloads/PlayerPayloads.cs):

- [x] `QuatPayload { float X, Y, Z, W; }` -- symmetric to the existing
      `Vec3Payload`.
- [x] `SetEntityPositionRequest { int EntityId; Vec3Payload Pos; }`
- [x] `SetEntityRotationRequest { int EntityId; QuatPayload Rot; }`
- [x] `MoveEntityRequest { int EntityId; Vec3Payload Direction; }`
- [x] `MoveForwardRequest { int EntityId; float Speed; }`
- [x] Move `DamageEntityRequest` from
      [../ESB/Payloads/PlayerPayloads.cs](../ESB/Payloads/PlayerPayloads.cs)
      into the new file and add `int EntityId` to it (was implicitly LocalPlayer
      before).
- [x] Reuse the existing `EntityIdRequest` from
      [../ESB/Payloads/StructurePayloads.cs](../ESB/Payloads/StructurePayloads.cs)
      for `MoveStop` and as the base for read ops -- do not duplicate.

### 3. EntityHandler.cs

Create [../ESB/TopicHandlers/EntityHandler.cs](../ESB/TopicHandlers/EntityHandler.cs)
mirroring the conventions of
[../ESB/TopicHandlers/PlayerHandler.cs](../ESB/TopicHandlers/PlayerHandler.cs)
and [../ESB/TopicHandlers/StructureHandler.cs](../ESB/TopicHandlers/StructureHandler.cs):

- [x] `public class EntityHandler` with `readonly ContextData _ctx` and a
      `public EntityHandler(ContextData ctx)` ctor.
- [x] `Register()` calls `_ctx.Bus.OnRequest("Entity", "<Op>", method)` once
      per op listed in Scope.
- [x] Private helper `IEntity GetEntity(int id)` modeled on
      `StructureHandler.GetStructureForEntity`: resolves
      `_ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield`, then
      `pf.Entities.TryGetValue(id, out entity)`. Throws
      `InvalidOperationException` with the standard message when not found.
- [x] `GetProperties` follows `PlayerHandler.Properties`: look up entity,
      dispatch via `_ctx.MainThreadRunner.RunOnMainThread`, use the local
      `JToken S(Func<object>)` safe-getter wrapper, build a `JObject` with
      `Id, Name, Type (ToString), Position (Vec), Forward (Vec), Rotation (Vec),
      IsLocal, IsProxy, IsPoi, Faction (FactionDataJson), BelongsTo, DockedTo,
      HasStructure (entity.Structure != null)`.
- [x] `List` iterates `pf.Entities` and emits
      `MessageHelpers.Tabular(columns, rows)`. Columns:
      `{ EntityId, Name, Type, FactionId, FactionGroup, BelongsTo, DockedTo, X, Y, Z, IsPoi }`.
      Skip null entries with the same defensive `if (e == null) continue` used
      in PlayfieldHandler.
- [x] `SetPosition` parses with `env.PayloadAs<SetEntityPositionRequest>()`,
      runs on main thread, sets
      `entity.Position = new Vector3(req.Pos.X, req.Pos.Y, req.Pos.Z)`, returns
      `{ "ok": true }`.
- [x] `SetRotation` parses with `env.PayloadAs<SetEntityRotationRequest>()`,
      assigns `entity.Rotation = new Quaternion(req.Rot.X, ...)`, returns
      `{ ok }`. (`MessageHelpers.ParseQuat` is the JToken-path equivalent for
      callers that prefer raw JObject parsing.)
- [x] `DamageEntity` parses `DamageEntityRequest`, looks up the entity by
      `req.EntityId`, calls `entity.DamageEntity(req.DamageAmount, req.DamageType)`
      on the main thread. This corrects the pre-existing behavior in
      `PlayerHandler.DamageEntity` which damages LocalPlayer regardless of
      payload.
- [x] `Move` / `MoveForward` / `MoveStop` parse their respective payloads,
      look up the entity, dispatch on the main thread, call the corresponding
      IEntity method, return `{ "ok": true }`.
- [x] Every handler method is `async Task<string>` (or `Task<string>` when no
      main-thread hop is needed) and wraps its body in `try { ... } catch
      (Exception ex) { return MessageHelpers.ExceptionJson(ex); }`.
- [x] Input parsing uses direct casts `(int)payload["EntityId"]` per the
      project rule; never `.Value<T>()`.

### 4. Remove Player/DamageEntity

- [x] Delete the `OnRequest("Player", "DamageEntity", ...)` registration in
      `PlayerHandler.Register`.
- [x] Delete the `DamageEntity` method body from PlayerHandler.
- [x] Remove the `DamageEntityRequest` class from `PlayerPayloads.cs` (moved
      to EntityPayloads in step 2).

### 5. Bus registration

- [x] Add `new EntityHandler(_ctx).Register();` to
      [../ESB/BusService/SubscriptionHandler.cs](../ESB/BusService/SubscriptionHandler.cs)
      in `SubscribeAll`, between PlayerHandler and StructureHandler so the
      registration ordering is Player -> Entity -> Structure -> Playfield.

### 6. Integration tests

Create `ESBTests/TopicHandlerTests/Test_Entity_Integration.cs` mirroring
[../ESBTests/TopicHandlerTests/Test_Player_Integration.cs](../ESBTests/TopicHandlerTests/Test_Player_Integration.cs):

- [x] `[Trait("Category", "Integration")]` class scaffold;
      `SBTestClient.ConnectAsync()` per test; tolerate the dedicated-server
      "no LocalPlayer" Error path the same way Player tests do
      (`if (payload["Error"] == null) ... else Assert.NotNull(Error)`).
- [x] `Properties_KnownEntity_ReturnsCoreFields` -- uses
      `KnownState.BaseEntityId`; asserts non-null
      `Id, Name, Type, Position.X, Faction.Id, HasStructure`.
- [x] `Properties_MissingEntityId_ReturnsError`.
- [x] `Properties_UnknownEntity_ReturnsError` -- send `EntityId: 999999`.
- [x] `List_ReturnsTabularEntities` -- assert
      `payload["Entities"]["Columns"]` and `Rows` exist; column header includes
      `BelongsTo` and `FactionId`.
- [x] `SetPosition_SamePos_ReturnsOk` -- read current Position then write it
      back; assert `ok` is true.
- [x] `SetRotation_Identity_ReturnsOk` -- send `{X:0,Y:0,Z:0,W:1}`.
- [x] `DamageEntity_ZeroDamage_ReturnsOkOrError` -- replaces the Player-scope
      test currently in `Test_Player_Integration.cs`; delete that one.
- [x] `Move_ZeroVector_ReturnsOk`, `MoveForward_ZeroSpeed_ReturnsOk`,
      `MoveStop_ReturnsOk` -- exercise each method op once.

### 7. Verification

- [ ] `dotnet build` clean.
- [ ] `dotnet test --filter "Category=Integration"` against a running game
      client with `KnownState.BaseEntityId` populated; tolerate dedicated-server
      Error responses where expected.
- [ ] Smoke-check live: send `Entity/List`, pick an EntityId from the result,
      send `Entity/GetProperties` with that id, send `Entity/SetPosition` to
      nudge it, visually confirm in-game.

---

## Conventions reaffirmed (so the implementation does not drift)

- .NET 4.8 / C# 7.3: no `!`, no nullable refs, no switch expressions, no
  range/index.
- JToken access: `(int)payload["X"]`, never `payload["X"].Value<int>()`.
- No XML doc comments; one short inline comment only when the why is
  non-obvious.
- All response text and code is plain 7-bit ASCII.
- Tabular payloads use `MessageHelpers.Tabular(columns, rows)`; rows are
  arrays of scalars in column order.
- Errors: `MessageHelpers.ExceptionJson(ex)` for caught exceptions;
  `MessageHelpers.ErrorJson("msg")` for known/expected conditions.
