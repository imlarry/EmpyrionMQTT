# ESB Testing Strategy

## Overview

Testing divides into three tiers with different requirements and tooling:

| Tier | Label | Needs game? | Save state | Run command |
|------|-------|-------------|------------|-------------|
| 1 — Unit | _(no filter)_ | No | N/A | `dotnet test` |
| 2 — Integration | `Category=Integration` | Yes (co-op or SP) | Read-only — safe anytime | `dotnet test --filter "Category=Integration"` |
| 3 — Destructive | `Category=Integration_Destructive` | Yes (co-op) | **Requires a restored baseline save** | `dotnet test --filter "Category=Integration_Destructive"` |

---

## Tests as API discovery

Integration tests serve a second purpose beyond regression coverage: they are the living record of what the ESB API surface actually does in a running game.

**Every registered handler method has at least one test.** When a test fails there are two valid outcomes — both are progress:

1. **Implement the missing code** — the handler exists but the underlying API call isn't wired up yet.
2. **Document a known boundary** — the API genuinely doesn't support this in the tested context. The test *passes* when ESB fails gracefully with a specific, parseable error.

A test that asserts `X` with `{"Error":"API unavailable on client"}` is a passing test. It documents a capability boundary and will automatically fail if the API becomes available in a future game version — prompting re-evaluation.

Never skip a test. If a handler returns an access violation, assert the exact error ESB returns. If ESB doesn't return a meaningful error yet, improve the handler first.

---

## Co-op topology — why it matters

Running a **co-op session** on a single machine starts three game processes, all connecting to the local MQTT broker. This is the only configuration where V1 and V2 can both be tested in the same run.

```
[Co-op session on 127.0.0.1]

  DedicatedServer process
    - V1 (EmpyrionModBase) initializes here — the only place V1 is available
    - V2 (IMod) also runs here — DedicatedServer scope
    - MQTT AppId: "DedicatedServer"

  PlayfieldServer process (one per loaded playfield)
    - V2 only — playfield-scoped entity data
    - MQTT AppId: "PlayfieldServer" (or playfield-specific variant)

  Client process (the local player's game client)
    - V2 only — local player data, UI, client-side events
    - MQTT AppId: "Client"
```

**Consequence for tests:**
- V2 tests targeting `Client` AppId work in both SP and co-op.
- V1 tests targeting `DedicatedServer` AppId only work in co-op — V1 is silent in SP.
- `KnownState.V1AppId = "DedicatedServer"` must always be used for V1 handler requests.

---

## Tier 1 — Unit Tests

No game required. Target pure logic: serialization, helpers, config parsing.

```bash
dotnet test ESBTests/ESBTests.csproj
```

**What belongs here:**
- Config deserialization (`ESBConfig`, `MQTThost`)
- `MessageHelpers` — `Vec()`, `ParseVec3()`, `ParseVecInt3()` round-trip fidelity
- JSON payload shape validation for known request/response pairs
- Any logic that can be exercised without Unity or the broker

**What does NOT belong here:** Anything that touches `IModApi`, `IPlayfield`, `IPlayer`, or `EmpyrionModBase` — those are Unity/game objects and cannot be instantiated outside the game process.

---

## Tier 2 — Integration Tests (read-only and idempotent)

Tagged `[Trait("Category","Integration")]`. Safe to run against any live game session without risk to save state.

**Includes:**
- All read operations (GetInfo, GetCredits, GetInventory, List, …)
- Idempotent writes that leave state identical to how it was found:
  - `AddCredits` with `Credits: 0.0`
  - `SetCredits` with the player's current balance (read → set same value)
  - `Block.SetDamage` with `Damage: 0`

**Run:**
```bash
dotnet test ESBTests/ESBTests.csproj --filter "Category=Integration"
```

**Monitor traffic in a separate terminal:**
```bash
mosquitto_sub -t "Client/#" -v
mosquitto_sub -t "DedicatedServer/#" -v
```

---

## Tier 3 — Destructive Integration Tests

Tagged `[Trait("Category","Integration_Destructive")]`. These tests modify player inventory, credits, stats, or position in ways that are not self-reverting if the test fails. They require a **restored baseline save** before each run.

**Includes:**
- `V1.Player.GetAndRemoveInventory` — clears the player's inventory
- `V1.Player.SetInventory` — replaces the player's inventory entirely
- `V1.Player.AddItem` — adds an item to the player's bag (persists on failure)
- `V1.Player.SetInfo` — patches player health, food, XP, faction, etc.
- `V1.Player.ChangePlayfield` — moves the player to a different playfield
- `V1.Player.ItemExchange` — opens a blocking modal dialog on the player's screen

**Run:**
```bash
dotnet test ESBTests/ESBTests.csproj --filter "Category=Integration_Destructive"
```

### Self-restoring test pattern

Where possible, destructive tests restore state themselves. The pattern is:

```csharp
[Trait("Category", "Integration_Destructive")]
public class Test_Player_Destructive
{
    [Fact]
    public async Task GetAndRemoveInventory_ClearsInventory_ThenRestores()
    {
        await using var mqtt = await MqttTestClient.ConnectAsync();

        // Step 1: capture + clear
        var (removeTopic, removePayload) = await mqtt.RequestAsync(
            "V1.Player.GetAndRemoveInventory",
            $"{{\"EntityId\":{KnownState.PlayerEntityId}}}",
            appId: KnownState.V1AppId);

        Assert.True(removeTopic.StartsWith($"{KnownState.V1AppId}/R/"));
        var inv = removePayload["Data"] as JObject;
        Assert.NotNull(inv);

        // Step 2: assert baseline inventory matches KnownState
        Assert.Equal(KnownState.Baseline.ToolbeltSlot0ItemId,
            inv["toolbelt"]![0]!["id"]!.Value<int>());

        // Step 3: restore — re-set the inventory we just captured
        var restorePayload = $"{{\"PlayerId\":{KnownState.PlayerEntityId}," +
            $"\"Toolbelt\":{inv["toolbelt"]!.ToString(Newtonsoft.Json.Formatting.None)}," +
            $"\"Bag\":{inv["bag"]!.ToString(Newtonsoft.Json.Formatting.None)}}}";

        var (restoreTopic, _) = await mqtt.RequestAsync(
            "V1.Player.SetInventory", restorePayload,
            appId: KnownState.V1AppId);

        Assert.True(restoreTopic.StartsWith($"{KnownState.V1AppId}/R/"));
    }
}
```

If the test fails at the assertion step, the inventory is already cleared. This is acceptable — the test run ends and you restore from the baseline save before the next run.

### Tests that cannot self-restore

`ChangePlayfield` and `ItemExchange` (modal dialog) cannot be reliably self-reverted from within the test. These tests assert only that ESB handled the request without throwing, and they are always run **last** in the destructive suite.

---

## The baseline save

### What the baseline save must contain

The save captures a precise game state. These facts are also encoded in `KnownState.cs` so tests can assert against them.

| Attribute | Requirement | KnownState constant (to add) |
|---|---|---|
| Player entity ID | Known, stable across reloads | `PlayerEntityId` (exists) |
| Player credits | A known round number | `Baseline.Credits` |
| Player toolbelt slot 0 | A specific item (id + count) | `Baseline.ToolbeltSlot0ItemId`, `Baseline.ToolbeltSlot0Count` |
| Player bag slot 0 | A specific item (id + count) | `Baseline.BagSlot0ItemId`, `Baseline.BagSlot0Count` |
| Player playfield | "Akua" (or whatever the test playfield is) | `Playfield` (exists) |
| Player position | Known coordinates | `Baseline.PlayerPos` |
| Test base (structure) | Placed, with known entity ID | `BaseEntityId` (exists) |
| Named devices on base | LCD, Light, Teleporter, Constructor, Fridge | all (exist) |
| AddItem test item | A cheap, stackable item — confirm its ID | `Baseline.TestItemId` |

### Save game location

Empyrion dedicated server saves live at:

```
C:\Program Files (x86)\Steam\steamapps\common\
  Empyrion - Dedicated Server\Saves\Games\<SaveName>\
```

The baseline archive is a full copy of that `<SaveName>\` folder stored at:

```
ESBTests/TestSave/Baseline/   ← committed to source control (or stored separately if large)
```

---

## Restore workflow

### Before running Tier 3 tests

```
1. Exit the co-op game session completely (all three processes must stop).
2. Run: Scripts/Restore-TestSave.ps1
   → Copies ESBTests/TestSave/Baseline/ → dedicated server saves folder
3. Start the co-op session.
4. Log in with the test player — confirm position and inventory match KnownState.Baseline.
5. dotnet test --filter "Category=Integration_Destructive"
```

### Standard development loop

```
dotnet test --filter "Category=Integration"          ← run anytime, no restore needed

# When adding/verifying destructive handlers:
Scripts/Restore-TestSave.ps1                         ← restore
(restart co-op session)
dotnet test --filter "Category=Integration_Destructive"
```

---

## Deliverable assignments

These are the artifacts you need to create to make Tier 3 testing operational. None of them are code — they are game-side and script-side work.

---

### Assignment 1 — Baseline Save Archive

**What:** A known-state dedicated server save folder, committed or stored alongside the repo.

**Steps:**
1. Start a co-op session with the existing test save.
2. Confirm all `KnownState` entities are in place (base, devices, player entity ID).
3. Set the player's inventory to a fixed, documented state:
   - Toolbelt slot 0: a specific item + count (write down the item ID)
   - Bag slot 0: a different item + count
   - All other slots: empty (or a known pattern)
4. Set the player's credits to a known round number (e.g., 10000).
5. Position the player at `KnownState.PlayerSpawnPos`.
6. Save and exit the game.
7. Copy the entire `<SaveName>\` folder from the dedicated server saves directory to `ESBTests/TestSave/Baseline/`.
8. Update `KnownState.cs` with the `Baseline.*` constants documented above.

**Deliverable:** `ESBTests/TestSave/Baseline/` directory + updated `KnownState.cs`

---

### Assignment 2 — Restore Script

**What:** `Scripts/Restore-TestSave.ps1` — stops any running dedicated server process, copies the baseline save over the active save, and confirms completion.

**Minimum viable script:**
```powershell
# Scripts/Restore-TestSave.ps1
param(
    [string]$SaveName = "ESBTest",
    [string]$BaselineDir = "$PSScriptRoot\..\ESBTests\TestSave\Baseline",
    [string]$ServerSaveDir = "C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Dedicated Server\Saves\Games"
)

$targetDir = Join-Path $ServerSaveDir $SaveName

# Safety: confirm game is not running
$running = Get-Process -Name "EmpyrionServer","EmpyrionPlayfieldServer","Empyrion-Dedicated" -ErrorAction SilentlyContinue
if ($running) {
    Write-Error "Dedicated server is still running. Exit the co-op session first."
    exit 1
}

Write-Host "Restoring baseline save to $targetDir ..."
if (Test-Path $targetDir) { Remove-Item $targetDir -Recurse -Force }
Copy-Item $BaselineDir -Destination $targetDir -Recurse
Write-Host "Restore complete. Start the co-op session now."
```

**Deliverable:** `Scripts/Restore-TestSave.ps1`

---

### Assignment 3 — Item ID Catalog (minimal)

**What:** Identify the numeric item IDs for two items to use in destructive tests:

| Purpose | Requirement | Where to find it |
|---|---|---|
| `Baseline.TestItemId` | A cheap, stackable, non-quest item. Iron Ingot, food, or ammo. | Use the in-game item selector, or check [Empyrion item ID list online](https://empyrion.gamepedia.com/). |
| `Baseline.ToolbeltSlot0ItemId` | Whatever you place in slot 0 of the baseline save. | Read back via `V1.Player.GetInventory` after building the baseline. |
| `Baseline.BagSlot0ItemId` | Whatever you place in bag slot 0 of the baseline save. | Same. |

**Deliverable:** Three item IDs confirmed and added to `KnownState.cs`.

---

### Assignment 4 — KnownState.Baseline nested class

**What:** Add a `Baseline` nested static class to `KnownState.cs` to hold the state facts that destructive tests assert against. Example shape:

```csharp
public static class Baseline
{
    public const double Credits         = 10000.0;

    // Toolbelt slot 0
    public const int ToolbeltSlot0ItemId  = ???;   // fill in after building the save
    public const int ToolbeltSlot0Count   = ???;

    // Bag slot 0
    public const int BagSlot0ItemId       = ???;
    public const int BagSlot0Count        = ???;

    // A safe, cheap item to add via V1.Player.AddItem in tests
    public const int TestItemId           = ???;
    public const int TestItemCount        = 1;

    // Player start position in the baseline save
    public const string PlayerPos = "{\"x\":-155.3,\"y\":53.1,\"z\":29.3}";
}
```

**Deliverable:** `KnownState.Baseline` class with all constants filled.

---

## Known State reference

The full game state the tests assume is documented in `ESBTests/Infrastructure/KnownState.cs`.

**Current baseline: VNS Akua base on Akua moon**

| Constant | Value | Description |
|----------|-------|-------------|
| `AppId` | `"Client"` | ESB process ID for V2 client-side handlers |
| `V1AppId` | `"DedicatedServer"` | ESB process ID for all V1 handlers |
| `Playfield` | `"Akua"` | Active playfield |
| `BaseEntityId` | `5320` | Test base entity ID |
| `BaseName` | `"VNS Akua"` | Structure name |
| `PlayerEntityId` | `1042` | Local player entity ID |
| `LeverSwitchBlock` | `{X:2,Y:130,Z:1}` | Struct-space lever switch position |
| `SignalName` | `"Fridge"` | Signal name emitted by lever switch |
| `DeviceName1` | `"Constructor"` | Named constructor device |
| `DeviceName2` | `"Fridge"` | Named fridge/container device |
| `LcdName` | `"InfoLcd"` | Named LCD panel |
| `LightName` | `"Light"` | Named light block |
| `TeleporterName` | `"Teleport"` | Named teleporter pad |
| `BaseGlobalPos` | `{X:-156.5,Y:51.0,Z:50.5}` | Approximate global coords of base |
| `PlayerSpawnPos` | `{X:-155.3,Y:53.1,Z:29.3}` | Player spawn above the base |

---

## Integration test coverage

| Test class | Category | Handler prefix |
|------------|----------|----------------|
| `ESBTests.V2.IApplication.Test_Application_Integration` | Integration | `V2.Application.*` |
| `ESBTests.V2.IBlock.Test_Block_Integration` | Integration | `V2.Block.*` |
| `ESBTests.V2.IContainer.Test_Container_Integration` | Integration | `V2.Container.*` |
| `ESBTests.V2.IGui.Test_Gui_Integration` | Integration | `V2.Gui.*` |
| `ESBTests.V2.ILcd.Test_Lcd_Integration` | Integration | `V2.Lcd.*` |
| `ESBTests.V2.IPlayer.Test_Player_Integration` | Integration | `V2.Player`, `V2.Player.*` |
| `ESBTests.V2.IPlayfield.Test_Playfield_Integration` | Integration | `V2.Playfield.*` |
| `ESBTests.V2.IStructure.Test_Structure_Integration` | Integration | `V2.Structure.*` |
| `ESBTests.V2.ITeleporter.Test_Teleporter_Integration` | Integration | `V2.Teleporter.*` |
| `ESBTests.V2.IUtilities.Test_Utilities_Integration` | Integration | `V2.Utilities.*` |
| `ESBTests.V1.IPlayer.Test_Player_Integration` | Integration | `V1.Player.*` (read + idempotent) |
| `ESBTests.V1.IPlayer.Test_Player_Destructive` | Integration_Destructive | `V1.Player.*` (mutating) |

---

## Coverage gaps

### V1.Player — handlers with no tests yet

Six handlers are implemented in [ESB/TopicHandlers/V1/Player.cs](../ESB/TopicHandlers/V1/Player.cs) but have no test coverage because they require a restored baseline save (Tier 3). They are blocked on Assignments 1–4 above.

| Handler | Why destructive | Self-restoring? |
|---|---|---|
| `V1.Player.GetAndRemoveInventory` | Atomically clears the player's inventory | Yes — capture result, call SetInventory to restore |
| `V1.Player.SetInventory` | Replaces toolbar and bag entirely | Yes — read first, set back on teardown |
| `V1.Player.AddItem` | Permanently adds item to bag | Partially — can remove with SetInventory afterward |
| `V1.Player.ItemExchange` | Opens a blocking modal dialog on player's screen | No — requires player interaction to dismiss |
| `V1.Player.SetInfo` | Patches health, food, XP, faction, etc. | Yes — read PlayerInfo first, patch back same values |
| `V1.Player.ChangePlayfield` | Teleports player to a different playfield immediately | No — requires player to travel back |

`ItemExchange` and `ChangePlayfield` should run last in the destructive suite and are effectively manual-confirm tests.

---

### V1 object groups — no handler code or tests

These groups are defined in [Docs/V1ApiObjectModel.md](V1ApiObjectModel.md) but have no ESB handler implementations and no tests. Each group needs a new handler class under `ESB/TopicHandlers/V1/` and a corresponding test file under `ESBTests/V1/`.

| Group | Proposed handler class | Proposed topics | Notes |
|---|---|---|---|
| Entity | `ESB.TopicHandlers.V1.Entity` | `V1.Entity.GetPosAndRot`, `.Teleport`, `.ChangePlayfield`, `.Destroy`, `.Spawn`, `.SetName`, `.Export`, `.NewId` | Mix of read and destructive |
| Structure | `ESB.TopicHandlers.V1.Structure` | `V1.Structure.ListGlobal`, `.Update`, `.Touch`, `.BlockStats` | `ListGlobal` is read-only and high-value |
| Playfield | `ESB.TopicHandlers.V1.Playfield` | `V1.Playfield.List`, `.Stats`, `.Load`, `.EntityList` | `List` and `Stats` are read-only |
| Server | `ESB.TopicHandlers.V1.Server` | `V1.Server.Stats`, `.ConsoleCommand`, `.BannedPlayers` | All read-only or low-risk |
| Faction | `ESB.TopicHandlers.V1.Faction` | `V1.Faction.List`, `.AlliancesAll`, `.AlliancesByFaction` | All read-only |
| Message | `ESB.TopicHandlers.V1.Message` | `V1.Message.ToPlayer`, `.ToAll`, `.ToFaction`, `.Dialog` | All mutating (sends UI to player) |
| Blueprint | `ESB.TopicHandlers.V1.Blueprint` | `V1.Blueprint.Finish`, `.Resources` | `Finish` is destructive (completes factory build) |

Priority order for implementation: **Server → Faction → Structure.ListGlobal → Playfield.List** (all read-only, low risk, immediately useful). Entity and Message come after destructive testing infrastructure is in place.

---

## Adding tests for a new handler

1. Choose the right tier:
   - Read-only or idempotent → `[Trait("Category","Integration")]`
   - Mutating, not self-restoring → `[Trait("Category","Integration_Destructive")]`
2. Create the test file under `ESBTests/V1/` or `ESBTests/V2/` matching the handler prefix.
3. Tag the class with the appropriate `[Trait]`.
4. Use `MqttTestClient.RequestAsync()` — one client instance per test (`await using`).
5. Pass `appId: KnownState.V1AppId` for any V1 handler.
6. Assert `topic` prefix (`R/` for success, `X/` for exception).
7. Assert `payload` keys — presence and type, not specific values where game state varies.
8. For self-restoring destructive tests: restore in the same test body, document what is dirty on failure.
