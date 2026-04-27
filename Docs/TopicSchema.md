# EmpyrionMQTT - Mod API MQTT Topic Standard

## 1. Overview

This document defines a structured MQTT topic schema for interacting with the EmpyrionMQTT Mod API. The schema supports request/response operations, server-pushed events, error reporting, and diagnostic logging across multiple simultaneous participants including players, playfield servers, a dedicated server, and open-ended agents.

The broker is **Mosquitto 2.1.2**, supporting MQTT 5.0, 3.1.1, and 3.1. This proposal targets **MQTT 5.0** and uses native protocol properties for routing and correlation rather than payload envelope fields.

---

## 2. Participants

Any process that connects to the bus is a **participant**. Each participant has a type and a unique connection ID assigned at connect time. The type appears in the topic as a routing segment; the connection ID isolates per-process messaging and enables precise wildcarding.

| Type | Description |
|---|---|
| `Client` | A game client (player session for single or multiplayer game) |
| `Pfs` | A PlayfieldServer process managing one playfield |
| `Ds` | The DedicatedServer process; application-level scope only |
| `Agent` | Any autonomous process operating on the bus (open-ended) |

The `Agent` type is intentionally generic. A specific agent's identity (name, purpose, capabilities) is described in the registry at connect time. The shared `Agent` segment keeps the topic namespace bounded while allowing any number of agent implementations to participate without coordination.

### 2.1 Connection Registry

At connect time each participant publishes its identity to a well-known registry topic with the **retain** flag set, so late-joining subscribers receive the current state immediately.

```
EMP/Registry/{connectionId}
  payload: {
    "type": "Pfs",
    "playfield": "Akua"
  }

EMP/Registry/{connectionId}
  payload: {
    "type": "Client",
    "playerId": 7,
    "playfield": "Akua"
  }

EMP/Registry/{connectionId}
  payload: {
    "type": "Agent",
    "name": "edna",
    "description": "Empyrion Data Network Assistant"
  }
```

Any participant can discover the `connectionId` for a given playfield, player, or agent by subscribing to `EMP/Registry/#` at startup.

---

## 3. Logical Scopes

The API exposes four scopes that map to how participants interact with the game:

| Scope | Description |
|---|---|
| `App` | Application-level data: game ticks, player list, playfield list, global structures |
| `Playfield` | Playfield-level data: entities, PvP state, planet type, solar system info |
| `Player` | Individual player state: health, position, inventory, credits, teleportation |
| `Structure` | Structure state and devices: fuel, shields, blocks, containers, LCDs, lights |

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
EMP/{participantType}/{connectionId}/{dir}/{scope}/{operation}
```

| Segment | Values | Notes |
|---|---|---|
| `EMP` | *(fixed)* | Namespace prefix for all Empyrion mod messages |
| `participantType` | `Client` \| `Pfs` \| `Ds` \| `Agent` | Identifies the class of participant; enables type-level wildcarding |
| `connectionId` | e.g. `q2e7` | Unique 4-char Base-36 ID assigned per connection; lowercase alphanumeric; isolates per-process messaging |
| `dir` | `Req` \| `Res` \| `Evt` \| `Err` \| `Log` | Direction: request, response, event, error, or log |
| `scope` | `App` \| `Playfield` \| `Player` \| `Structure` | The logical level of the operation |
| `operation` | `get/{prop}` \| `set/{prop}` \| `call/{method}` \| `{cid}` \| `{eventName}` \| `{level}` | For `Res` and `Err`, operation is the correlation ID; for `Evt` it is the event name; for `Log` it is the level |

Placing `{dir}` before `{scope}` means a single wildcard subscription covers all scopes and all sub-scope depths:

```
EMP/+/{connectionId}/Req/#    # all inbound requests regardless of scope
```

Entity IDs (player, structure) are always carried in the payload, not the topic. This keeps the topic structure uniform across all scopes and participants.

### 5.2 Device Sub-Scope

Devices within a structure (LCD panels, lights, containers, teleporters) add a `Device` segment after the scope:

```
EMP/{participantType}/{connectionId}/{dir}/Structure/Device/{deviceName}/{operation}
```

The structure entity ID is carried in the payload. Devices may be addressed by custom name or by position when no custom name is assigned:

```
-> pub  EMP/Pfs/g2w2/Req/Structure/Device/pos/set/Text
        MQTT properties:  Correlation Data = "x1y2"
        payload:          { "EntityId": 42, "Pos": {"X":4,"Y":2,"Z":7}, "Value": "Hull integrity nominal" }
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
  set MQTT Response Topic   = "EMP/{myType}/{myConnectionId}/Res/{scope}/{cid}"
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
  publish EMP/{myType}/{myConnectionId}/Err/{scope}/{cid}
    payload: { "code": "Timeout", "description": "No response received" }
  delete map[cid]
```

The `cid` appears in the **response topic itself**, which means each in-flight request has a unique response topic, the requester can subscribe to a single wildcard and route by `correlationData`, and no two responses can collide regardless of arrival order.

### 6.3 Wildcard Subscription for Responses

```
# Subscribe once; route all responses via correlationData property
EMP/{myType}/{myConnectionId}/Res/#

# Or subscribe to a single specific response
EMP/{myType}/{myConnectionId}/Res/App/m3n4
```

---

## 7. Examples

### 7.1 Read Application Game Ticks

```
-> pub  EMP/Ds/r56z/Req/App/get/GameTicks
        MQTT properties:  Response Topic   = "EMP/Agent/p41a/Res/App/a1b2"
                          Correlation Data = "a1b2"
        payload:          {}

<- sub  EMP/Agent/p41a/Res/App/a1b2
        MQTT properties:  Correlation Data = "a1b2"
        payload:          { "Value": 4823901 }
```

### 7.2 Set Player Credits

```
-> pub  EMP/Pfs/g2w2/Req/Player/set/Credits
        MQTT properties:  Response Topic   = "EMP/Client/9baq/Res/Player/c3d4"
                          Correlation Data = "c3d4"
        payload:          { "EntityId": 7, "Value": 5000 }

<- sub  EMP/Client/9baq/Res/Player/c3d4
        MQTT properties:  Correlation Data = "c3d4"
        payload:          { "ok": true }
```

### 7.3 Teleport a Player

```
-> pub  EMP/Pfs/g2w2/Req/Player/call/Teleport
        MQTT properties:  Response Topic   = "EMP/Client/9baq/Res/Player/e5f6"
                          Correlation Data = "e5f6"
        payload:          { "EntityId": 7, "Playfield": "Omicron", "Pos": [100,80,200], "Rot": [0,0,0] }

<- sub  EMP/Client/9baq/Res/Player/e5f6
        MQTT properties:  Correlation Data = "e5f6"
        payload:          { "ok": true }
```

### 7.4 Read LCD Text

```
-> pub  EMP/Pfs/g2w2/Req/Structure/Device/lcd_status/get/Lcd
        MQTT properties:  Response Topic   = "EMP/Agent/p41a/Res/Structure/g7h8"
                          Correlation Data = "g7h8"
        payload:          { "EntityId": 42, "Pos": {"X":4,"Y":2,"Z":7} }

<- sub  EMP/Agent/p41a/Res/Structure/g7h8
        MQTT properties:  Correlation Data = "g7h8"
        payload:          { "Text": "Hull integrity nominal", "FontSize": 20 }
```

### 7.5 Set LCD Text

```
-> pub  EMP/Pfs/g2w2/Req/Structure/Device/lcd_status/set/Text
        MQTT properties:  Response Topic   = "EMP/Client/9baq/Res/Structure/i9j0"
                          Correlation Data = "i9j0"
        payload:          { "EntityId": 42, "Pos": {"X":4,"Y":2,"Z":7}, "Value": "Warning: Low Fuel" }

<- sub  EMP/Client/9baq/Res/Structure/i9j0
        MQTT properties:  Correlation Data = "i9j0"
        payload:          { "ok": true }
```

### 7.6 Read a Light Color

```
-> pub  EMP/Pfs/g2w2/Req/Structure/Device/light_entry/get/Light
        MQTT properties:  Response Topic   = "EMP/Client/9baq/Res/Structure/m3n4"
                          Correlation Data = "m3n4"
        payload:          { "EntityId": 42, "Pos": {"X":1,"Y":0,"Z":3} }

<- sub  EMP/Client/9baq/Res/Structure/m3n4
        MQTT properties:  Correlation Data = "m3n4"
        payload:          { "Color": { "R": 255, "G": 128, "B": 0, "A": 255 }, "Intensity": 1.0 }
```

---

## 8. Events

Events are server-pushed and carry no correlation. Any subscriber may listen. Entity IDs are in the payload.

```
EMP/Pfs/g2w2/Evt/Playfield/EntityEntered
EMP/Pfs/g2w2/Evt/Player/HealthChanged
EMP/Pfs/g2w2/Evt/Structure/FuelLow
EMP/Client/9baq/Evt/App/GameStateChanged
```

Wildcard subscriptions aggregate events across connections:

```
EMP/Pfs/+/Evt/Player/#          # all player events across all Pfs
EMP/Pfs/+/Evt/Structure/FuelLow # fuel alerts across all playfields
EMP/+/+/Evt/Player/HealthChanged # health changes from any participant type
```

---

## 9. Errors

Errors use the `Err` direction with the `cid` as the operation segment, mirroring the response pattern. Any participant or observer subscribed to the `Err` topic can react - logging, alerting, or retrying.

```
<- sub  EMP/Pfs/g2w2/Err/Structure/e5f6
        MQTT properties:  Correlation Data = "e5f6"
        payload:          { "code": "DeviceLocked", "description": "Device is locked" }
```

Timeouts are emitted by the **requester** on their own `Err` topic:

```
<- sub  EMP/Client/9baq/Err/Player/c3d4
        payload:          { "code": "Timeout", "description": "No response received" }
```

Async callbacks that fail or do not return within the expected window emit on the `Err` topic so the pending map entry can be resolved.

---

## 10. Logging

Log messages use the `Log` direction with the level as the operation segment.

```
EMP/{participantType}/{connectionId}/Log/{scope}/{level}
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

Log publishing by `Client`, `Pfs`, and `Ds` participants is controlled by a config file on those processes. Agent participants self-manage their log publishing. Subscribers may filter by participant type, connection, scope, or level using standard MQTT wildcards:

```
EMP/+/+/Log/#          # all log messages from all participants
EMP/Pfs/+/Log/App/#    # all App-scope logs from all playfield servers
EMP/Agent/+/Log/#      # all logs from all agents
```

---

## 11. Design Notes

| Consideration | Decision |
|---|---|
| Participant model | Four types (`Client`, `Pfs`, `Ds`, `Agent`) as topic segments; `Agent` is shared and open-ended - specific identity lives in the registry |
| MQTT version | Targets MQTT 5.0 on Mosquitto 2.1.2; `Response Topic` and `Correlation Data` are protocol-level properties, not payload fields |
| Correlation | Implements the Correlation Identifier EIP (Hohpe & Woolf); pending map keyed by `cid`; timeout emits `Err` on requester's own topic |
| Connection isolation | `connectionId` ensures messages reach exactly one process; no unintended fan-out across participants |
| Segment order | `{dir}` precedes `{scope}` so a single wildcard `EMP/+/{connId}/Req/#` covers all scopes and all device sub-scope depths |
| Playfield context | Both `Client` and `Pfs` have a current playfield; participant type implies intent -- client-side vs. authoritative server-side representation |
| Async callbacks | Methods like `GetStructure` reply late to the `Response Topic`; callers set a timeout and emit `Err` on expiry |
| Entity IDs | Always in payload, never in topic; keeps topic structure uniform across all scopes and participant types |
| Device addressing | Devices sub-scoped under `Structure/Device` by custom name or `pos`; structure `EntityId` in payload; maps to `GetDevice<T>(name\|pos)` server-side |
| Registry | Published with retain flag; any participant can discover the full bus membership at startup via `EMP/Registry/#` |
| Log control | ESB-controlled participants toggled via config; agents self-manage; log level in topic segment for subscriber filtering |
