# ESB Testing Strategy

## Overview

Testing divides into two tiers with different requirements and tooling:

| Tier | What | Needs game? | Tooling |
|------|------|-------------|---------|
| 1 — Unit | Serialization, helpers, config parsing | No | xUnit / VS Code Test Explorer |
| 2 — Integration | Live MQTT round-trips against a running game | Yes | mosquitto CLI + payload files |

---

## Tier 1 — Unit Tests (ESBTests project)

The `ESBTests` project targets `net48`, uses xUnit, and is already discoverable by VS Code via the C# extension Test Explorer panel (`Ctrl+Shift+P` → "Test: Focus on Test Explorer View").

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
Anything that touches `IModApi`, `IPlayfield`, `IPlayer`, etc. — those are Unity/game objects and cannot be instantiated outside the game process.

---

## Tier 2 — Integration Tests (mosquitto + live game)

Integration tests require:
1. Mosquitto broker running (`mosquitto` — localhost default)
2. Game running with the ESB mod loaded
3. A player in the **test save** (see Known State below)

**Capture all traffic in a dedicated terminal before running any test:**
```bash
mosquitto_sub -t "Client/#" -v
```

### Directory layout

```
tests/
  IApplication/
    LocalPlayer.json
    ShowEntity.json
    Teleport.json
    GetPathFor.json
    SendChatMessage.json
    ...
  IGui/
    ShowGameMessage.json
    ShowGameMessage-prio.json
    ShowDialog.json
    IsWorldVisible.json
  IPlayfield/
    Info.json
    SpawnEntity.json
    SpawnPrefab.json
    RemoveEntity.json
    IsStructureDeviceLocked.json
    MoveEntity.json
  IPlayer/
    Stats.json
    SteamId.json
    Teleport-local.json
    Teleport-crossplayfield.json
  IStructure/
    Info.json
    GetDevicePositions.json
    GetBlock.json
    SetFaction.json
    GetSignalState.json
    ...
  scripts/
    run-all.sh
    run-IApplication.sh
    run-IGui.sh
    run-IPlayfield.sh
    run-IPlayer.sh
    run-IStructure.sh
  known-state.md
```

### Payload file format

Each `.json` file is a valid JSON object passed directly as the MQTT payload via `mosquitto_pub -f`.
Keys follow the handler's documented schema. All vector fields use structured objects:

```json
{ "Pos": {"X": 0.0, "Y": 100.0, "Z": 0.0} }
```

### Script convention

Each `run-I<Interface>.sh` follows the same pattern:

```bash
#!/usr/bin/env bash
# run-IGui.sh — requires live game + broker

TOPIC_BASE="Client/Q"
PAYLOAD="tests/IGui"
PUB() { mosquitto_pub -t "$TOPIC_BASE/$1/*/1" -f "$PAYLOAD/$2"; sleep 0.5; }

# Notify the tester via the game HUD before steps requiring in-game observation
NOTIFY() { mosquitto_pub -t "$TOPIC_BASE/Gui.ShowGameMessage/*/1" \
           -m "{\"Text\":\"TEST: $1\",\"Prio\":2,\"Duration\":8}"; sleep 1; }

NOTIFY "Starting IGui tests"
PUB Gui.IsWorldVisible     IsWorldVisible.json
PUB Gui.ShowGameMessage    ShowGameMessage.json
NOTIFY "A dialog should appear now"
PUB Gui.ShowDialog         ShowDialog.json
```

For steps that require a player action before the next command (e.g., entering a structure,
approaching an entity), use `Gui.ShowDialog` to gate progress — the script waits for the
`Client/I/Gui.ShowDialog/#` event before sending the next request.

---

## Known State (test save)

`tests/known-state.md` documents the test save so every run starts from a deterministic baseline.
Record the following after building the save:

```markdown
## Save: ESB_TestWorld

### Player start
- Playfield: TestFlats
- Position: {X:0, Y:100, Z:0}

### Structures
| Name | EntityId | StructureId | Notable devices |
|------|----------|-------------|-----------------|
| TestBase | 1001 | 2001 | Container at block {X:5,Y:2,Z:3}, LCD at {X:5,Y:3,Z:3} |
| TestShip | 1002 | 2002 | Fuel tank, teleporter |

### Entities (non-player)
| Name | EntityId | Type | Position |
|------|----------|------|----------|
| TestSlime | 1050 | Animal | {X:10,Y:100,Z:0} |
```

Entity IDs persist in the save file. Build the save once, document it, and commit
`known-state.md` so the test scripts can reference fixed IDs without discovery steps.

---

## GUI-driven test prompting

Because many integration tests require the tester to observe or perform an action in-game,
use `Gui.ShowGameMessage` (brief status) and `Gui.ShowDialog` (gated confirmation) to close
the loop without leaving the game window:

- **Before** a step that spawns or moves something visible: send a `ShowGameMessage` warning
- **After** the action: send another message confirming what the expected result is
- **For branching** (pass/fail confirmation from the tester): send a `ShowDialog` with
  Yes/No buttons; the script listens for the `Client/I/Gui.ShowDialog/#` event and branches

This keeps the tester in-game and minimises context-switching to the terminal.

---

## Adding tests for a new handler

1. Create the payload file(s) under `tests/I<Interface>/`
2. Add a `PUB` line to the corresponding `run-I<Interface>.sh`
3. If the handler has pure logic (parsing, serialization), add a Tier 1 xUnit fact to `ESBTests`
4. Update `tests/known-state.md` if the test requires a specific entity or structure
