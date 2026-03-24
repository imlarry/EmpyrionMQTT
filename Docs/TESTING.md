# ESB Testing Strategy

## Overview

Testing divides into two tiers with different requirements and tooling:

| Tier | What | Needs game? | Tooling |
|------|------|-------------|---------|
| 1 — Unit | Serialization, helpers, config parsing | No | xUnit / VS Code Test Explorer |
| 2 — Integration | Live MQTT round-trips against a running game | Yes | xUnit + MqttTestClient |

---

## Tier 1 — Unit Tests

The `ESBTests` project targets `net48`, uses xUnit, and is discoverable by VS Code via the
C# extension Test Explorer panel (`Ctrl+Shift+P` → "Test: Focus on Test Explorer View").

**Run from the terminal:**
```bash
dotnet test ESBTests/ESBTests.csproj
```

**What belongs here:**
- Config deserialization (`ESBConfig`, `MQTThost`) — already covered
- `MessageHelpers` — `Vec()`, `ParseVec3()`, `ParseVecInt3()` round-trip fidelity
- JSON payload structure — given a known JObject, does the response serializer produce the expected shape?
- Any pure logic that can be exercised without Unity or the broker

**What does NOT belong here:**
Anything that touches `IModApi`, `IPlayfield`, `IPlayer`, etc. — those are Unity/game objects
and cannot be instantiated outside the game process.

---

## Tier 2 — Integration Tests

Integration tests are xUnit facts tagged `[Trait("Category","Integration")]`. They use
`MqttTestClient` (in `ESBTests/Infrastructure/`) to publish a request and await the
response or exception over a live broker.

**Requirements:**
1. Mosquitto broker running (`mosquitto` — localhost:1883 default)
2. Game running with ESB mod loaded
3. A player in the test save (see Known State below)

**Run integration tests:**
```bash
dotnet test ESBTests/ESBTests.csproj --filter "Category=Integration"
```

**Skip integration tests (unit only):**
```bash
dotnet test ESBTests/ESBTests.csproj --filter "Category!=Integration"
```

**Capture all traffic while running (separate terminal):**
```bash
mosquitto_sub -t "Client/#" -v
```

### Directory layout

```
ESBTests/
  Infrastructure/
    KnownState.cs          — entity IDs, block positions, device names for the test save
    MqttTestClient.cs      — thin MQTT client: ConnectAsync(), RequestAsync()
  IApplication/
    Test_Application_Integration.cs
  IBlock/
    Test_Block_Integration.cs
  IContainer/
    Test_Container_Integration.cs
  IGui/
    Test_Gui_Integration.cs
  ILcd/
    Test_Lcd_Integration.cs
  IPlayer/
    Test_Player_Integration.cs
  IPlayfield/
    Test_Playfield_Integration.cs
  IStructure/
    Test_Structure_Integration.cs
```

### MqttTestClient usage

```csharp
await using var mqtt = await MqttTestClient.ConnectAsync();
var (topic, payload) = await mqtt.RequestAsync(
    "Structure.Info", $"{{\"EntityId\":{EID}}}");

Assert.StartsWith("Client/R/Structure.Info/", topic);
Assert.NotNull(payload["IsReady"]);
```

`RequestAsync` publishes to `Client/Q/{handler}/*/1` and awaits the first message on
`Client/R/{handler}/#` or `Client/X/{handler}/#`. Throws `TimeoutException` after 5 s.
Each test creates its own client instance (`await using`) so subscriptions do not bleed
across tests.

### Mutation tests

Handlers that change game state are tested with the minimum safe mutation (e.g., `SetDamage`
with `Damage:0` repairs rather than damages; `AddTankContent` with `Amount:0.0`). Where a
non-trivial mutation is required, the test accepts either `R` (success) or `X` (exception)
and verifies only that ESB handled the request without crashing.

---

## Known State (test save)

The test save state is documented in code at `ESBTests/Infrastructure/KnownState.cs`.
Build the save once, record the constants, and keep them in sync with the actual save.

**Current known state: VNS Akua base on Akua moon**

| Constant | Value | Description |
|----------|-------|-------------|
| `AppId` | `"Client"` | ESB source ID in client/singleplayer mode |
| `Playfield` | `"Akua"` | Active playfield name |
| `BaseEntityId` | `5320` | Entity ID of the test base |
| `BaseName` | `"VNS Akua"` | Structure name |
| `LeverSwitchBlock` | `{X:2,Y:130,Z:1}` | Struct-space position of a lever switch |
| `SignalName` | `"Fridge"` | Signal sent by the lever switch |
| `DeviceName1` | `"Constructor"` | Named constructor device |
| `DeviceName2` | `"Fridge"` | Named fridge (container) device |
| `BaseGlobalPos` | `{X:-83.5,Y:52.0,Z:-26.5}` | Approximate global position of the base |
| `LcdName` | `"InfoLcd"` | Custom name of LCD panel (must be placed on base) |
| `LcdBlock` | `{X:0,Y:131,Z:0}` | Struct-space position of LCD — **TODO: update** |
| `FridgeBlock` | `{X:0,Y:130,Z:2}` | Struct-space position of Fridge — **TODO: update** |

To find the actual position of a named device, run:
```bash
mosquitto_pub -t "Client/Q/Structure.GetDevicePositions/*/1" \
  -m "{\"EntityId\":5320,\"DeviceName\":\"Fridge\"}"
```

---

## Integration test coverage

| Test class | Tests | Handler prefix |
|------------|-------|----------------|
| `ESBTests.IApplication.Test_Application_Integration` | 12 | `Application.*` |
| `ESBTests.IBlock.Test_Block_Integration` | 7 | `Block.*` |
| `ESBTests.IContainer.Test_Container_Integration` | 5 | `Container.*` |
| `ESBTests.IGui.Test_Gui_Integration` | 4 | `Gui.*` |
| `ESBTests.ILcd.Test_Lcd_Integration` | 4 | `Lcd.*` |
| `ESBTests.IPlayer.Test_Player_Integration` | 3 | `Player.*` |
| `ESBTests.IPlayfield.Test_Playfield_Integration` | 3 | `Playfield.*` |
| `ESBTests.IStructure.Test_Structure_Integration` | 17 | `Structure.*` |

---

## Adding tests for a new handler

1. Create `ESBTests/I<Interface>/Test_<Interface>_Integration.cs`
2. Tag the class `[Trait("Category","Integration")]`
3. Add any required position or name constants to `KnownState.cs`
4. Use `MqttTestClient.RequestAsync()` — one client instance per test (`await using`)
5. Assert on `topic` prefix (`Client/R/` for success, `Client/X/` for exception)
6. Assert on `payload` keys — presence and type, not specific values where game state varies
7. For mutation handlers: use the minimum safe mutation or accept R|X and verify ESB handled it
