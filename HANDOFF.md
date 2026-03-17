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
  - `*` at position 4 is the multicast address (literal character, not an MQTT wildcard); ESB subscribes to `Client/Q/+/*/#`
  - To send a one-off request from mosquitto: `mosquitto_pub -t "Client/Q/{HandlerName}/*/1" -m '{...}'`
  - Use a unique value at position 5 (correlationId) when sending concurrent requests so responses don't cross-talk:
    publish to `Client/Q/{HandlerName}/*/{correlationId}`, subscribe to `Client/R/{HandlerName}/*/{correlationId}/#`
  - To receive all: `mosquitto_sub -t "Client/#" -v`

## Patterns — apply to every handler

### `S()` helper — safe property reads
Used where Unity API properties throw NRE when not applicable in current mode:
```csharp
JToken S(Func<object> getter) {
    try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
    catch { return JValue.CreateNull(); }
}
```
**Known throwing properties on the client mod:** `SteamOwnerId`, `Permission` — both return `null` silently.

### `MainThreadRunner` — Unity thread affinity
Unity game object properties **must** be read on the main thread.
`_ctx.MainThreadRunner.RunOnMainThread(async () => { ... })` queues work that `Update` processes on
the game thread. Any exception propagates back via `TaskCompletionSource`.
**Common miss:** calling `ModApi.Application.SendChatMessage()`, `ShowDialogBox()`, or any
`GetDevice<T>()` off the main thread → game crash.

### PascalCase convention
All ESB MQTT message keys use PascalCase. Eleon API structs (`GlobalStructureInfo`, `ItemStack`)
have genuinely lowercase fields (`id`, `name`, `pos`, `x/y/z`). **Never** use
`JsonConvert.SerializeObject` on API structs — always construct `JObject`/`JProperty` manually.

### `ShowEntity` — structure readiness guard
Structure properties split by safety:
- **Always returned:** `Id`, `IsReady`, `MinPos`, `MaxPos`, `PlayerCreatedSteamId`, `CoreType`, `SizeClass`, `LastVisitedTicks`
- **Only when `IsReady == true`:** `IsPowered`, `DamageLevel`, `BlockCount`, `DeviceCount`, `Fuel`, etc.
Accessing computed properties before `IsReady` causes divide-by-zero crashes.

### `TraceEntity` — radar use case
- Per-tick cache check: entity drops from cache → `{"Status":"EntityLost","EntityId":...}` on `E` and stops
- Normal timeout → `{"Status":"TraceExpired","EntityId":...}` on `I`
- Position updates → `{"EntityId":..., "Position":{X,Y,Z}}` on `E`

### `GetDevice<T>` — ILcd / IContainer / ILight etc.
All device sub-interfaces addressed by `EntityId` + struct-space `Pos`.
Always null-check the return — returns `null` if no device of that type is at the position.
Return `X` with a descriptive error rather than NRE-crashing.

### Dialog action events
Dialog button-press results are events (`MessageClass.Event`), not informational messages.
Subscribe to `Client/E/Gui.ShowDialog/#` to receive button results asynchronously.

---

## Completed handlers

### Application.cs ✓
`WindowInfo`, `ShowEntity`, `LocalPlayer`, `GetStructure`, `GetStructures`, `Teleport`,
`TraceEntity`, `SendChatMessage`, `ShowDialogBox`

**`LocalPlayer` properties returned:** `EntityId`, `Name`, `SteamId`, `SteamOwnerId`, `StartPlayfield`,
`Origin`, `Permission`, `Ping`, `UpgradePoints`, `IsPoi`, `BelongsTo`, `DockedTo`, `Toolbar`, `Bag`,
`Rotation {X,Y,Z,W}` — all via `S()` + `MainThreadRunner`.

### Player.cs ✓
`Player.Teleport`, `Player.SteamId`, `Player.Stats`

**`Player.Stats` properties returned:** `Health`/`Max`, `Oxygen`/`Max`, `Stamina`/`Max`, `Food`/`Max`,
`Radiation`/`Max`, `BodyTemp`/`Max`, `Credits`, `ExperiencePoints`, `UpgradePoints`, `Kills`, `Died`,
`Ping`, `HomeBaseId`, `IsPilot`, `FactionData {Group,Id}`, `FactionRole`, `CurrentStructureId`,
`DrivingEntityId`, `Forward {X,Y,Z}`, `IsLocal`, `IsProxy`

### Gui.cs ✓
`ShowGameMessage`, `ShowDialog` (button results fire on `E`), `IsWorldVisible`

### Playfield.cs ✓
`Playfield.Info`, `SpawnEntity`, `IsStructureDeviceLocked`

**`Playfield.Info` properties returned:** `Name`, `PlayfieldType`, `PlanetType`, `PlanetClass`,
`SolarSystemName`, `SolarSystemCoordinates {X,Y,Z}`, `IsPvP`

**Known cosmetic quirk:** `PosInStructure` echoes using `VectorInt3.ToString()` → `"X/Y/Z"` (slashes),
while input expects `"X,Y,Z"` (commas). Value round-trips correctly — cosmetic only.

### Structure.cs ✓
`Structure.Info`, `Structure.Tanks`, `Structure.AddTankContent`, `Structure.GetAllCustomDeviceNames`,
`Structure.GetDevicePositions`, `Structure.GetDockedVessels`, `Structure.GetPassengers`,
`Structure.GetBlockSignals`, `Structure.GetControlPanelSignals`, `Structure.GetSignalState`,
`Structure.GetSignalReceivers`, `Structure.GetSendSignalName`, `Structure.StructToGlobalPos`,
`Structure.GlobalToStructPos`, `Structure.SetFaction`

**Note:** `IBlock` operations live under `Block.*`, not `Structure.*`, because `IBlock` is a
factory-accessed sub-interface (`structure.GetBlock(pos)`), consistent with `Lcd.*` and `Container.*`.
`IStructureTank` stays under `Structure.*` because tanks are named properties of `IStructure`.

### Block.cs ✓
`Block.Get`, `Block.Set`, `Block.SetDamage`, `Block.GetTextures`, `Block.SetTextures`,
`Block.SetTextureForWholeBlock`, `Block.GetColors`, `Block.SetColors`, `Block.SetColorForWholeBlock`,
`Block.GetSwitchState`, `Block.SetSwitchState`, `Block.SetLockCode`

### Lcd.cs ✓
`Lcd.Get`, `Lcd.SetText`, `Lcd.SetTextColor`, `Lcd.SetBackgroundColor`, `Lcd.SetFontSize`

Colors serialized as `{"R":f,"G":f,"B":f,"A":f}` (Unity `Color`, 0.0–1.0 range).
**Note:** freshly placed LCDs may return `null` from `GetText()` — always null-coalesce to `string.Empty`.

### Light.cs ✓
`Light.Get`, `Light.SetColor`, `Light.SetIntensity`, `Light.SetRange`,
`Light.SetLightType`, `Light.SetBlinkData`, `Light.SetSpotAngle`

Colors serialized as `{"R":f,"G":f,"B":f,"A":f}` (Unity `Color`, 0.0–1.0 range).
`LightType` serialized as its enum name string (e.g. `"Point"`, `"Spot"`) — use `Light.Get`
to discover valid values for the placed light before calling `SetLightType`.
Set all blink values to `0.0` to disable blinking.

### Teleporter.cs ✓
`Teleporter.Get`, `Teleporter.Set`

`TargetData` fields: `TargetEntityNameOrGroup` (e.g. `"deviceName@structName"`),
`TargetPlayfield`, `TargetSolarSystemName`, `Origin` (byte, `255` = no restriction).
Works for both `ITeleporter` (teleporter pad) and `IPortal` (portal device) —
`GetDevice<ITeleporter>` matches both since `IPortal` inherits `ITeleporter`.

### Container.cs ✓
`Container.Get`, `Container.Contains`, `Container.GetTotalItems`, `Container.AddItems`,
`Container.RemoveItems`, `Container.Clear`, `Container.SetContent`

`ItemStack` fields normalized from Eleon lowercase (`id`, `count`, `slotIdx`, `ammo`, `decay`) to
ESB PascalCase (`Id`, `Count`, `SlotIdx`, `Ammo`, `Decay`).
**Note:** item type `0` is invalid for `GetTotalItems` and `AddItems` — always use a real item type
from `Container.Get` content when constructing requests.

---

## Integration test coverage
All 57 pre-existing integration tests pass. Light and Teleporter tests require game setup.
`dotnet test --filter "Category=Integration"`

| Test class | Tests | Status |
|------------|-------|--------|
| `ESBTests.IApplication.Test_Application_Integration` | 12 | ✓ |
| `ESBTests.IBlock.Test_Block_Integration` | 7 | ✓ |
| `ESBTests.IContainer.Test_Container_Integration` | 5 | ✓ |
| `ESBTests.IGui.Test_Gui_Integration` | 4 | ✓ |
| `ESBTests.ILight.Test_Light_Integration` | 8 | pending game setup (place light named "Light") |
| `ESBTests.ILcd.Test_Lcd_Integration` | 4 | ✓ |
| `ESBTests.IPlayer.Test_Player_Integration` | 3 | ✓ |
| `ESBTests.IPlayfield.Test_Playfield_Integration` | 3 | ✓ |
| `ESBTests.IStructure.Test_Structure_Integration` | 19 | ✓ |
| `ESBTests.ITeleporter.Test_Teleporter_Integration` | 4 | pending game setup (place teleporter named "Teleport") |

**`MqttTestClient` design note:** each `RequestAsync` call uses a unique GUID fragment as `correlationId`
at topic position 5. The ESB's `#` wildcard covers it. Subscribing to `R/{handler}/*/{correlationId}/#`
ensures concurrent test classes can't steal each other's responses.

---

## Remaining work

### Top-level IModApi interfaces (not started)

| Interface | IModApi property | Notes |
|-----------|-----------------|-------|
| `IPda` | `IModApi.PDA` | Restricted — always null for client mods. See block comment in Pda.cs and fail-fast tests in ESBTests/IPda/. |

**Intentionally excluded:**
- `INetwork` — inter-mod binary packet routing; ESB is already the network layer via MQTT, redundant
- `IScript` — runtime C# compilation inside game process; code-injection attack surface, excluded by design
- `ISoundPlayer` — Unity 3D audio requires AssetBundles; replaced by EDNA SoundManager (see below)

### Gaps in existing handlers

#### IPlayer — missing methods
- `DamageEntity(int entityId, float damage)`
- `MoveForward(float seconds)`, `Move(float x, float y, float z)`, `MoveStop()`
- `LoadFromDSL(string dsl)`

#### IPlayfield — missing methods/properties
- `LockStructureDevice(int structId, VectorInt3 pos, bool locked, Action<bool> callback)`
- `GetTerrainHeightAt(float x, float z)`
- `AddVoxelArea(...)`, `MoveVoxelArea(...)`, `RemoveVoxelArea(...)`
- `Players[get]`, `Entities[get]` — enumerate players/entities on playfield

#### IStructure — missing
- `GetDevices(string deviceTypeName)` — enumerate all positions of a device type
- `Entity[get]`, `Pilot[get]`

### Device sub-interfaces

| Interface | Device type | Status |
|-----------|------------|--------|
| `ILight` | Light blocks — colour, intensity, blinking | ✓ (`Light.*`) |
| `IPortal` | Teleporter portal | ✓ (covered by `Teleporter.*` via `GetDevice<ITeleporter>`) |
| `ITeleporter` | Teleporter pad | ✓ (`Teleporter.*`) |

### EDNA SoundManager (replaces ISoundPlayer)

Sound is owned by EDNA, not ESB. EDNA subscribes to game events from ESB and plays audio via NAudio/MediaFoundation.

| Topic | Payload | Notes |
|-------|---------|-------|
| `Sound.Play` | `{Clip, Volume?, Pos?, Loop?, Id?}` | `Clip` = name from local manifest |
| `Sound.Stop` | `{Id}` | Stop a looping clip |
| `Sound.List` | `{}` | Returns available clip names |

`Pos` is optional; if supplied EDNA computes distance attenuation and stereo panning from `Application.LocalPlayer`.

### Suggested implementation order
1. ~~`Pda.*`~~ — restricted; fail-fast tests retained as reference
2. ~~`Light.*`~~ — done
3. ~~`Teleporter.*`~~ — done
4. `Entity.*` — `IEntity` mutations: `DamageEntity`, `MoveForward`/`Move`/`MoveStop` (via `Player.*` gaps)

---

## Tech debt

| # | Location | Issue |
|---|----------|-------|
| 1 | All device handlers (`Lcd`, `Light`, `Teleporter`, `Container`, `Block`) | `MessageHelpers.Vec(pos)` in error strings uses `JToken.ToString()` default (pretty-printed), producing `\r\n` and indentation inside the JSON error value. Fix: add a `MessageHelpers.VecInline(pos)` that returns a single-line `{"X":n,"Y":n,"Z":n}` string and use it in all error message interpolations. |
| 2 | `ESBTests/ITeleporter/Test_Teleporter_Integration.cs` `InitializeAsync` | `?? string.Empty` fallback converts `null` TargetData fields to `""` before the Set round-trip test, so it sends `""` instead of `null`. Game accepts both, but not a faithful round-trip. Fix: store as `string?` and emit JSON `null` when the value is null. |
| ~~3~~ | ~~All `IAsyncLifetime` test classes using device discovery~~ | ✓ Fixed: `InitializeAsync` wraps discovery in try/catch and stores `_skipReason`; each `[Fact]` calls `Assert.Skip(_skipReason)` if set. Device-not-found now shows a clean skip message instead of NRE cascade. Applied to `Test_Lcd_Integration`, `Test_Light_Integration`, `Test_Teleporter_Integration`. |

---

## Crosscheck checklist (per handler)
1. Read handler file
2. Read matching API `.md` from `Modding Doc/api/`
3. Check: are all API methods/properties subscribed?
4. Check: are responses using the correct `MessageClass`?
5. Check: are Unity properties read on `MainThreadRunner`?
6. Check: are nullable/throwing properties guarded?
7. Check: do fire-and-forget `Task.Run` blocks have try-catch?
8. Test each handler with `mosquitto_pub -t "Client/Q/{Handler}/*/1" -m "{...}"`
