# EDNA Skills Overview

Each section below covers one skill (or core component): its purpose, the MQTT messages it
subscribes to, and the messages it sends or requests.

Subscription filters use MQTT wildcards: `+` matches one segment, `#` matches the rest.

---

## Core Service (EdnaService)

**Purpose**: Orchestrates game-lifecycle events. Detects when the game process starts and stops,
connects/disconnects MQTT, starts all enabled skills, and relays game events (GameEnter, GameExit,
PlayfieldLoaded) to skills and to the Lua script host.

**Subscribes**

| Filter | Handler |
|---|---|
| `ESB/+/+/App/Evt/GameEnter` | Connects MQTT, starts skills and Lua host, restores workspace documents |
| `ESB/+/+/App/Evt/GameExit` | Stops Lua host, closes game session UI |
| `ESB/+/+/Playfield/Evt/Loaded` | Forwards solar system / playfield / coordinates to all `IPlayfieldObserver` skills |

**Publishes**: None directly. Lifecycle events are forwarded to the Lua host via
`LuaScriptHost.Broadcast("on_game_enter", ...)` and `LuaScriptHost.Broadcast("on_game_exit", ...)`.

---

## FloorMap

**Purpose**: Captures a 2-D floor plan of the structure the player is currently inside. Triggered
by the Ctrl+Shift+R hotkey. Executes a three-step request sequence to locate the player, convert
world coordinates to structure-local coordinates, and scan two adjacent floor levels (walls and
floor tiles). The resulting map is rendered as an overhead view and saved to disk.

**Subscribes**: None. The skill receives playfield context through the `IPlayfieldObserver`
callback (`OnPlayfieldLoaded`), not via MQTT subscription.

**Requests** (via `RequestAsync` -- awaits a reply)

| Scope | Operation | Payload | Purpose |
|---|---|---|---|
| `Player` | `GetProperties` | `{"Properties":["Position","CurrentStructureId","CurrentStructureEntityId"]}` | Get player world position and the entity ID of the structure they are inside |
| `Structure` | `GlobalToStructPos` | `{"EntityId":<int>,"Pos":{"X":...,"Y":...,"Z":...}}` | Convert world position to structure-local block coordinates to determine the floor Y level |
| `Structure` | `ScanFloor` (x2) | `{"EntityId":<int>,"Y":<int>}` | Scan block data at Y (walls) and Y-1 (floor tiles) in parallel |

**Publishes**: None beyond the implicit Req messages above.

---

## GalaxyMap

**Purpose**: Displays a 2-D map of all star systems loaded from the game's `galaxy.csv` export.
Shows the player's current position and supports filtering to systems within 30 light-years.
Purely a display skill -- it reads a file and reacts to playfield changes.

**Subscribes**: None.

**Publishes**: None.

**Data source**: `{SaveGamePath}/Content/Mods/GalaxyExtract/galaxy.csv`

---

## ThreatRadar

**Purpose**: Maintains a real-time directional threat display (N/E/S/W quadrants) around the
player. Requests a continuous Feeds.Scan feed on playfield entry, then classifies each Predator-
faction entity by distance (Imminent < 20 m, Close < 50 m, Near < 100 m) and bearing relative to
the player's facing direction.

**Subscribes**

| Filter | Handler |
|---|---|
| `ESB/+/+/App/Evt/PlayfieldEntered` | Triggers an initial Feeds.Scan request when entering any playfield |
| `ESB/+/+/App/Evt/Feeds.Scan` | Receives continuous position/entity snapshots; re-arms the feed when a terminal status event arrives |

**Publishes**

| Scope | Type | Name | Payload | Purpose |
|---|---|---|---|---|
| `App` | `Req` | `Feeds.Scan` | `{"Duration":300,"RefreshRate":2}` | Start a 300-second continuous scan feed at 2 Hz |

---

## ScriptEditor

**Purpose**: File-based UI for creating and editing Lua scripts. Manages the directory tree under
the game save's EDNA scripting folder, with syntax highlighting and hot-reload. Purely a UI
editing tool -- it does not subscribe to or publish any MQTT messages itself.

**Subscribes**: None.

**Publishes**: None.

**Data source**: `{SaveGamePath}/Content/Mods/ESB/EDNA/skills/scripting/`

---

## Lua Script Host

**Purpose**: Loads and manages isolated Lua script engines from the scripting directory.
Provides hot-reload via `FileSystemWatcher`, a shared blackboard for inter-script coordination,
and three API namespaces to each script: `mqtt`, `log`, and `bb`. Broadcasts C#-side lifecycle
events (`on_game_enter`, `on_game_exit`) to all running scripts.

**Subscribes**: None from the host itself. Individual Lua scripts may subscribe to arbitrary
filters via `mqtt.subscribe(filter, callback)`.

**Publishes** (diagnostic / error reporting)

| Scope | Type | Name | Payload |
|---|---|---|---|
| `App` | `Log` | `LuaScriptHost.Error` | `{"Script":"<name>","Error":"<message>"}` |
| `App` | `Log` | `LuaScriptHost.WatcherError` | `{"Error":"<message>"}` |

**Lua `log` API publishes**

| Scope | Type | Name | Payload |
|---|---|---|---|
| `App` | `Log` | `LuaLog.Warn` | `{"Script":"<name>","Message":"<text>"}` |
| `App` | `Log` | `LuaLog.Error` | `{"Script":"<name>","Message":"<text>"}` |

**Lua `mqtt` API**: Exposes `mqtt.publish(scope, msgType, name, payload)` (maps to
`IMessenger.SendAsync`) and `mqtt.subscribe(topicFilter, callback)` (maps to
`IMessenger.SubscribeEventAsync`). Both let scripts interact with the bus directly; topic strings
are authored by the script author.
