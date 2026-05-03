# EmpyrionMQTT - Mod API MQTT Topic Standard

## 1. Overview

This document defines a structured MQTT topic schema for interacting with the EmpyrionMQTT Mod API. The schema supports request/response operations, server-pushed events, error reporting, and diagnostic logging across multiple simultaneous participants including players, playfield servers, a dedicated server, and open-ended agents.

The broker is **Mosquitto 2.1.2**, supporting MQTT 5.0, 3.1.1, and 3.1. This mod requires **MQTT 5.0** and uses native protocol properties for routing and correlation rather than payload envelope fields.

---

## 2. Participants

Any process that connects to the bus is a **participant**. Each participant has a type and a unique connection ID assigned at connect time. The type appears in the topic as a routing segment; the connection ID isolates per-process messaging and enables precise wildcarding.

| Type | Description |
|---|---|
| `Client` | A game client (player session for single or multiplayer game) |
| `Pfs` | A PlayfieldServer process managing one playfield |
| `Ds` | The DedicatedServer process; application-level scope only |
| `{user-defined}` | Any external participant uses its own type string (e.g. `edna` for EDNA) |

External participants (tools, automation scripts, companion apps) pass their own participant type string to `ConnectAsync`. That string appears as-is in the `participantType` topic segment. Each participant owns its type -- no coordination or shared moniker is needed.

### 2.1 Connection Registry

At connect time each participant publishes its identity to a well-known registry topic with the **retain** flag set, so late-joining subscribers receive the current state immediately.

```
ESB/Registry/{connectionId}
  payload: {
    "type": "Pfs",
    "playfield": "Akua"
  }

ESB/Registry/{connectionId}
  payload: {
    "type": "Client",
    "playerId": 7,
    "playfield": "Akua"
  }

ESB/Registry/{connectionId}
  payload: {
    "type": "edna",
    "description": "Empyrion Data Network Assistant"
  }
```

Any participant can discover the `connectionId` for a given playfield, player, or agent by subscribing to `ESB/Registry/#` at startup.

---

## 3. Logical Scopes

The API exposes five scopes that map to how participants interact with the game:

| Scope | Description |
|---|---|
| `App` | Application-level data: game ticks, player list, playfield list, global structures |
| `Playfield` | Playfield-level data: entities, PvP state, planet type, solar system info |
| `Player` | Individual player state: health, position, inventory, credits, teleportation |
| `Structure` | Structure state: fuel, shields, blocks, tanks, signals, docked vessels |
| `Device` | Structure-mounted devices: LCD panels, lights, containers, teleporters |

Note that both `Client` and `Pfs` participants have a current playfield context. The participant type implies the intent - a `Client` request on a playfield scope targets the local client-side representation; a `Pfs` request targets the authoritative server-side representation.

---

## 4. Entity Types

Every entity in the game has a type value. The following table lists all known types, followed by logical groupings useful for filtering and subscription patterns.

| Value | Name | Value | Name |
|---|---|---|---|
| 0 | Unknown | 15 | EnemyDrone |
| 1 | Player | 16 | PlayerBackpack |
| 2 | BA | 17 | DropContainer |
| 3 | CV | 18 | ExplosiveDevice |
| 4 | SV | 19 | PlayerBike |
| 5 | HV | 20 | PlayerBikeFolded |
| 6 | AstRes | 21 | Asteroid |
| 7 | AstVoxel | 22 | Civilian |
| 8 | EscapePod | 23 | Cyborg |
| 9 | Animal | 24 | TroopTransport |
| 10 | Turret | 25 | MissionContainer |
| 11 | Item | 26 | Proxy |
| 12 | PlayerDrone | 27 | TerrainPlaceable |
| 13 | Trader | 28 | AstRingPlanet |
| 14 | UnderRes | 29 | NPCFighter |

### 4.1 Logical Groupings

| Group | Members | Notes |
|---|---|---|
| **Structures** | `BA`, `CV`, `SV`, `HV` | Player/faction-built; expose `IStructure`, devices, fuel, shields |
| **Player** | `Player` | The human-controlled entity |
| **Player Belongings** | `PlayerBackpack`, `PlayerBike`, `PlayerBikeFolded`, `PlayerDrone`, `EscapePod`, `TerrainPlaceable` | Spawned by or belonging to a specific player; `BelongsTo` identifies owner |
| **NPCs** | `Animal`, `Civilian`, `Trader`, `Cyborg`, `NPCFighter`, `TroopTransport` | Living entities with AI behavior; includes neutral and hostile |
| **Hostile Devices** | `EnemyDrone`, `Turret`, `ExplosiveDevice` | Stationary or autonomous combat entities |
| **Loot & Containers** | `Item`, `DropContainer`, `MissionContainer` | World-placed or drop-spawned collectables |
| **World / Resources** | `Asteroid`, `AstRes`, `AstVoxel`, `AstRingPlanet`, `UnderRes` | Environmental and resource entities; generally read-only, no ownership |
| **System / Meta** | `Unknown`, `Proxy` | Internal or virtual entities; typically filtered out |

---

## 5. Topic Format

### 5.1 Base Structure

```
ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
```

| Segment | Values | Notes |
|---|---|---|
| `ESB` | *(fixed)* | Namespace prefix for all Empyrion Service Bus mod messages |
| `participantType` | `Client` \| `Pfs` \| `Ds` \| `{user-defined}` | Identifies the participant type; game-side types are fixed; external participants define their own string |
| `connectionId` | e.g. `q2e7` | Unique 4-char Base-36 ID assigned per connection; lowercase alphanumeric; isolates per-process messaging |
| `scope` | `App` \| `Playfield` \| `Player` \| `Structure` \| `Device` \| `Registry` | The logical level of the operation; routes to the matching `{scope}Handler` |
| `msgType` | `Req` \| `Res` \| `Evt` \| `Err` \| `Log` | Message type: request, response, event, error, or log |
| `operation` | `PascalCase` or `PascalCase.MetaOp` | For `Req`, a PascalCase method name with an optional dot-suffix meta-operation (e.g. `GetPathFor.Describe`); for `Res` and `Err`, mirrors the `Req` operation unchanged; for `Evt`, the event name; for `Log`, the operation name |

Placing `{scope}` first groups all traffic for a logical domain together, making it natural to subscribe by scope across all message types:

```
ESB/+/{connectionId}/App/#    # all App-scope messages (Req, Evt, Log, Err) from one participant
ESB/+/{connectionId}/+/Req/#  # all inbound requests regardless of scope
```

Entity IDs (player, structure) are always carried in the payload, not the topic. This keeps the topic structure uniform across all scopes and participants.

### 5.2 Device Scope

Structure-mounted devices (LCD panels, lights, containers, teleporters) use `Device` as a first-class scope. The topic stays at the standard 6-segment depth; the structure entity ID and device name are carried in the payload:

```
-> pub  ESB/Pfs/g2w2/Device/Req/SetText
        MQTT properties:  Correlation Data = "x1y2"
        payload:          { "EntityId": 42, "DeviceName": "StatusLcd", "Value": "Hull integrity nominal" }
```

---

## 6. Request / Response

### 6.1 MQTT 5.0 Correlation

The system uses native **MQTT 5.0 packet properties** for routing and correlation rather than payload envelope fields. Payloads carry only the actual data; routing and correlation are handled at the protocol layer.

| MQTT 5.0 Property | Role |
|---|---|
| `Response Topic` | The topic the responder publishes to |
| `Correlation Data` | An opaque identifier echoed back in the response |

The responder echoes both properties back in the response message unchanged. Any participant that receives the request and holds these properties can reply correctly without out-of-band coordination.

### 6.2 Correlation Identifier Pattern

The request/response model implements the **Correlation Identifier** pattern (Hohpe & Woolf, *Enterprise Integration Patterns*). Each in-flight request is tracked in a **pending request map** keyed by correlation ID.

```
on send:
  generate cid
  set MQTT Response Topic   = "ESB/{myType}/{myConnectionId}/{scope}/Res/{operation}"
  set MQTT Correlation Data = cid
  store map[cid] = { context, timer }
  publish request

on receive:
  cid   = message.correlationData
  entry = map[cid]
  clear entry.timer
  handle response
  delete map[cid]

on timeout:
  publish ESB/{myType}/{myConnectionId}/{scope}/Err/{operation}
    payload: { "code": "Timeout", "description": "No response received" }
  delete map[cid]
```

The response and error topics mirror the request topic exactly -- only `{msgType}` changes (`Req` -> `Res` or `Err`). In-flight requests are distinguished by `Correlation Data` alone; the pending map is keyed by `cid`.

### 6.3 Wildcard Subscription for Responses

```
# Subscribe once; route all responses via correlationData property
ESB/{myType}/{myConnectionId}/+/Res/#

# Or subscribe to responses for a specific operation
ESB/{myType}/{myConnectionId}/App/Res/GameTicks
```

---

## 7. Examples

### 7.1 Read Application Game Ticks

```
-> pub  ESB/Ds/r56z/App/Req/GameTicks
        MQTT properties:  Response Topic   = "ESB/Ds/r56z/App/Res/GameTicks"
                          Correlation Data = "a1b2"
        payload:          {}

<- sub  ESB/Ds/r56z/App/Res/GameTicks
        MQTT properties:  Correlation Data = "a1b2"
        payload:          { "Value": 4823901 }
```

### 7.2 Set Player Credits

```
-> pub  ESB/Pfs/g2w2/Player/Req/Credits
        MQTT properties:  Response Topic   = "ESB/Pfs/g2w2/Player/Res/Credits"
                          Correlation Data = "c3d4"
        payload:          { "EntityId": 7, "Value": 5000 }

<- sub  ESB/Pfs/g2w2/Player/Res/Credits
        MQTT properties:  Correlation Data = "c3d4"
        payload:          { "ok": true }
```

### 7.3 Teleport a Player

```
-> pub  ESB/Pfs/g2w2/Player/Req/Teleport
        MQTT properties:  Response Topic   = "ESB/Pfs/g2w2/Player/Res/Teleport"
                          Correlation Data = "e5f6"
        payload:          { "EntityId": 7, "Playfield": "Omicron", "Pos": [100,80,200], "Rot": [0,0,0] }

<- sub  ESB/Pfs/g2w2/Player/Res/Teleport
        MQTT properties:  Correlation Data = "e5f6"
        payload:          { "ok": true }
```

### 7.4 Read LCD Text

```
-> pub  ESB/Pfs/g2w2/Device/Req/GetLcd
        MQTT properties:  Response Topic   = "ESB/Pfs/g2w2/Device/Res/GetLcd"
                          Correlation Data = "g7h8"
        payload:          { "EntityId": 42, "DeviceName": "StatusLcd" }

<- sub  ESB/Pfs/g2w2/Device/Res/GetLcd
        MQTT properties:  Correlation Data = "g7h8"
        payload:          { "Text": "Hull integrity nominal", "FontSize": 20 }
```

### 7.5 Set LCD Text

```
-> pub  ESB/Pfs/g2w2/Device/Req/SetText
        MQTT properties:  Response Topic   = "ESB/Pfs/g2w2/Device/Res/SetText"
                          Correlation Data = "i9j0"
        payload:          { "EntityId": 42, "DeviceName": "StatusLcd", "Value": "Warning: Low Fuel" }

<- sub  ESB/Pfs/g2w2/Device/Res/SetText
        MQTT properties:  Correlation Data = "i9j0"
        payload:          { "ok": true }
```

### 7.6 Read a Light Color

```
-> pub  ESB/Pfs/g2w2/Device/Req/GetLight
        MQTT properties:  Response Topic   = "ESB/Pfs/g2w2/Device/Res/GetLight"
                          Correlation Data = "m3n4"
        payload:          { "EntityId": 42, "DeviceName": "EntryLight" }

<- sub  ESB/Pfs/g2w2/Device/Res/GetLight
        MQTT properties:  Correlation Data = "m3n4"
        payload:          { "Color": { "R": 255, "G": 128, "B": 0, "A": 255 }, "Intensity": 1.0 }
```

---

## 8. Events

Events are server-pushed and carry no correlation. Any subscriber may listen. Entity IDs are in the payload.

```
ESB/Pfs/g2w2/Playfield/Evt/EntityEntered
ESB/Pfs/g2w2/Player/Evt/HealthChanged
ESB/Pfs/g2w2/Structure/Evt/FuelLow
ESB/Client/9baq/App/Evt/GameStateChanged
```

Wildcard subscriptions aggregate events across connections:

```
ESB/Pfs/+/Player/Evt/#           # all player events across all Pfs
ESB/Pfs/+/Structure/Evt/FuelLow  # fuel alerts across all playfields
ESB/+/+/Player/Evt/HealthChanged  # health changes from any participant type
```

---

## 9. Errors

Errors use the `Err` direction with the `cid` as the operation segment, mirroring the response pattern. Any participant or observer subscribed to the `Err` topic can react - logging, alerting, or retrying.

```
<- sub  ESB/Pfs/g2w2/Structure/Err/e5f6
        MQTT properties:  Correlation Data = "e5f6"
        payload:          { "code": "DeviceLocked", "description": "Device is locked" }
```

Timeouts are emitted by the **requester** on their own `Err` topic:

```
<- sub  ESB/Client/9baq/Player/Err/c3d4
        payload:          { "code": "Timeout", "description": "No response received" }
```

Async callbacks that fail or do not return within the expected window emit on the `Err` topic so the pending map entry can be resolved.

---

## 10. Logging

Log messages use the `Log` direction with the level as the operation segment.

```
ESB/{participantType}/{connectionId}/{scope}/Log/{level}
```

### 10.1 Payload Structure

```json
{
  "message": "Device state updated",
  "timestamp": "2025-04-25T14:32:01.000Z",
  "cid": "i9j0"
}
```

`cid` is included when the log entry relates to an in-flight request, enabling correlation of log output with request/response activity. It is omitted for entries not associated with a specific operation.

### 10.2 Enable / Disable

Log publishing by `Client`, `Pfs`, and `Ds` participants is controlled by a config file on those processes. External participants self-manage their log publishing. Subscribers may filter by participant type, connection, scope, or level using standard MQTT wildcards:

```
ESB/+/+/+/Log/#        # all log messages from all participants
ESB/Pfs/+/App/Log/#    # all App-scope logs from all playfield servers
ESB/edna/+/+/Log/#     # all logs from the edna participant
```

---

## 11. Design Notes

| Consideration | Decision |
|---|---|
| Participant model | Three game-side types (`Client`, `Pfs`, `Ds`) as fixed topic segments; external participants define their own type string; each participant's type appears as-is in the topic |
| MQTT version | Targets MQTT 5.0 on Mosquitto 2.1.2; `Response Topic` and `Correlation Data` are protocol-level properties, not payload fields |
| Correlation | Implements the Correlation Identifier EIP (Hohpe & Woolf); pending map keyed by `cid`; timeout emits `Err` on requester's own topic |
| Connection isolation | `connectionId` ensures messages reach exactly one process; no unintended fan-out across participants |
| Segment order | `{scope}` precedes `{msgType}` so all traffic for a logical domain is naturally grouped; a two-segment wildcard `ESB/+/{connId}/+/Req/#` covers all inbound requests |
| Playfield context | Both `Client` and `Pfs` have a current playfield; participant type implies intent -- client-side vs. authoritative server-side representation |
| Async callbacks | Methods like `GetStructure` reply late to the `Response Topic`; callers set a timeout and emit `Err` on expiry |
| Entity IDs | Always in payload, never in topic; keeps topic structure uniform across all scopes and participant types |
| Device addressing | `Device` is a first-class scope; `EntityId` and `DeviceName` are carried in the payload; each operation routes to `DeviceHandler` |
| Registry | Published with retain flag; any participant can discover the full bus membership at startup via `ESB/Registry/#` |
| Log control | ESB-controlled participants toggled via config; agents self-manage; log level in topic segment for subscriber filtering |
