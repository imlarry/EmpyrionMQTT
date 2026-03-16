# ESB TopicHandlers — API Crosscheck Session Handoff

## What we're doing
Systematically crosschecking each file in `ESB/TopicHandlers/` against the Empyrion Modding API docs,
fixing bugs, and verifying each fix via live MQTT testing.

## Context
- **ESB** is an Empyrion Galactic Survival client/server mod that bridges the game to an MQTT broker
- **IModApi** is the root API object; `Application`, `Player`, `Playfield`, `GUI` etc. hang off it
- **API docs** are pre-extracted to `Modding Doc/api/` — one `.md` file per interface/class/struct.
  Read the relevant `.md` before crosschecking a handler.
- **MQTT topic format:** `{appId}/{msgClass}/{handler}/{clientId}/{seqNum}`
  - `appId` = `Client` (when game is in client/singleplayer mode)
  - msgClass: `Q`=Request, `R`=Response, `E`=Event, `I`=Information, `X`=Exception
  - To send a request from mosquitto: publish to `Client/Q/{HandlerName}/*/1` (the `*` is the multicast clientId)
  - To receive all: `mosquitto_sub -t "Client/#" -v`

## Completed: Application.cs
All 6 issues found and fixed:

| # | Issue | Fix |
|---|-------|-----|
| 1 | `WindowInfo` replied with `MessageClass.Request` instead of `Response` | Changed to `MessageClass.Response` |
| 2 | `ShowEntity` accessed `Structure.DamageLevel` etc. before `IsReady` — divide-by-zero crash | Split structure JSON: static fields always, computed fields only when `IsReady == true` |
| 3 | `Teleport` called `LocalPlayer.Teleport()` with no null guard — NRE if no active player | Added null check with descriptive error message |
| 4 | `TraceEntity` published position updates as `MessageClass.Request` — ESB self-subscribed and re-processed them in a loop | Changed to `MessageClass.Event`; also moved `entity.Position` read to `MainThreadRunner`, added try-catch to surface silent Task.Run failures, switched to per-tick cache check so `EntityLost` event fires if entity is killed mid-trace |
| 5 | `LocalPlayer` response missing ~12 IPlayer properties | Added all missing properties (`SteamId`, `SteamOwnerId`, `StartPlayfield`, `Origin`, `Permission`, `Ping`, `UpgradePoints`, `IsPoi`, `BelongsTo`, `DockedTo`, `Toolbar`, `Bag`); wrapped all property reads in `MainThreadRunner`; used `S()` helper (see below) |
| 6 | `ShowEntity` threw NRE on `entity.Faction.ToString()` when entity was mid-load transition | Wrapped in local try-catch, returns `null` for `Faction` if getter throws |

## Patterns established

### `S()` helper — safe property reads
Used in `LocalPlayer` to handle Unity API properties that throw NRE when not applicable in current mode:
```csharp
JToken S(Func<object> getter) {
    try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
    catch { return JValue.CreateNull(); }
}
```
**Known throwing properties on the client mod:** `SteamOwnerId` (not applicable without Steam Family Sharing),
`Permission` (not implemented in client mode). Both return `null` silently.

### `MainThreadRunner` — Unity thread affinity
Unity game object properties must be read on the main thread.
`_ctx.MainThreadRunner.RunOnMainThread(async () => { ... await Task.CompletedTask; })` queues work that
`Update` processes on the game thread. Any exception propagates back via `TaskCompletionSource`.

### `TraceEntity` — radar use case
- Designed for tracking predator entities on a radar display
- Per-tick cache check: if entity drops from cache (killed/unloaded), sends `{"Status":"EntityLost","EntityId":...}` on `E` and stops
- Normal timeout sends `{"Status":"TraceExpired","EntityId":...}` on `I`
- Position updates send `{"EntityId":..., "Position":{X,Y,Z}}` on `E`

### `ShowEntity` — structure readiness
Structure properties split by safety:
- **Always returned:** `Id`, `IsReady`, `MinPos`, `MaxPos`, `PlayerCreatedSteamId`, `CoreType`, `SizeClass`, `LastVisitedTicks`
- **Only when `IsReady == true`:** `IsPowered`, `DamageLevel`, `BlockCount`, `DeviceCount`, `Fuel`, etc.

## Completed: Player.cs
All 3 issues found and fixed:

| # | Issue | Fix |
|---|-------|-----|
| 1 | `Teleport` accessed `LocalPlayer` without null guard | Added null check, returns descriptive exception |
| 2 | `SteamId` accessed `LocalPlayer` without null guard | Same null check pattern |
| 3 | ~20 IPlayer properties never exposed (Health, Oxygen, Food, etc.) | Added `Player.Stats` handler returning full vitals payload via `S()` helper + MainThreadRunner |

**`Player.Stats` properties returned:** `Health`/`HealthMax`, `Oxygen`/`OxygenMax`, `Stamina`/`StaminaMax`, `Food`/`FoodMax`, `Radiation`/`RadiationMax`, `BodyTemp`/`BodyTempMax`, `Credits`, `ExperiencePoints`, `UpgradePoints`, `Kills`, `Died`, `Ping`, `HomeBaseId`, `IsPilot`, `FactionData` `{Group, Id}`, `FactionRole`, `CurrentStructureId`, `DrivingEntityId`, `Forward` `{X,Y,Z}`, `IsLocal`, `IsProxy`

**Verified:** `Application.LocalPlayer` confirmed working with full rich payload (includes all the Application.cs Fix #5 properties).
**Verified:** `Player.Stats` — confirmed working. `mosquitto_pub -t "Client/Q/Player.Stats/*/1" -m "{}"`

## Completed: Gui.cs
No bugs found. All 3 IGui methods covered and correctly implemented:
- `ShowGameMessage` ✓ — correct sig, MainThreadRunner, Response class
- `ShowDialog` ✓ — correct sig, MainThreadRunner; dialog action events fire on `Client/I/Gui.ShowDialog/#`
- `IsWorldVisible` ✓ — MainThreadRunner, Response class

## Completed: Playfield.cs
3 issues found and fixed:

| # | Issue | Fix |
|---|-------|-----|
| 1 | `SpawnEntity` captured `entityId` return value but never included it in the response JSON | Added `EntityId` property to response |
| 2 | `IsStructureDeviceLocked` called `ClientPlayfield` directly on the MQTT thread, not main thread | Wrapped in `MainThreadRunner`; also changed `StructureId`/`IsStructureDeviceLocked` from `.ToString()` strings to typed int/bool |
| 3 | No handler for IPlayfield read-only descriptor properties | Added `Playfield.Info` handler: `Name`, `PlayfieldType`, `PlanetType`, `PlanetClass`, `SolarSystemName`, `SolarSystemCoordinates {X,Y,Z}`, `IsPvP` — all read on MainThreadRunner, wrapped in `S()` helper |

**Verified (live tests on Akua/Ellyon):**
- `Playfield.Info` → `{"Name":"Akua","PlayfieldType":"Moon","PlanetType":"TemperateStarter",...,"IsPvP":false}` ✓
- `SpawnEntity` → `{"EntityId":5315,"EntityType":"Slime"}` ✓ (EntityLoaded event also fired before response)
- `IsStructureDeviceLocked` → `{"StructureId":12345,"PosInStructure":"VectorInt3 0/0/0","IsStructureDeviceLocked":false}` ✓

**Known cosmetic quirk:** `PosInStructure` echoes back using `VectorInt3.ToString()` which formats as `"X/Y/Z"` (slashes), while the input expects `"X,Y,Z"` (commas). Not a bug — the value round-trips correctly.

**Not yet implemented (acknowledged):** `AddVoxelArea`, `MoveVoxelArea`, `RemoveVoxelArea`, `SpawnTestPlayer`, `RemoveTestPlayer`, `GetTerrainHeightAt`, `Players[get]`, `Entities[get]`, `LockStructureDevice` (requires async callback).

## Remaining handlers to crosscheck

| File | Key API interfaces to check against |
|------|-------------------------------------|
| `Edna.cs` | (EDNA-specific, check separately) |

## Crosscheck checklist (per handler)
1. Read handler file
2. Read matching API `.md` from `Modding Doc/api/`
3. Check: are all API methods/properties subscribed?
4. Check: are responses using the correct `MessageClass`?
5. Check: are Unity properties read on `MainThreadRunner`?
6. Check: are nullable/throwing properties guarded?
7. Check: do fire-and-forget `Task.Run` blocks have try-catch?
8. Test each handler with `mosquitto_pub -t "Client/Q/{Handler}/*/1" -m "{...}"`
