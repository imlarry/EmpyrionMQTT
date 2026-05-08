# EmpyrionMQTT -- Messaging Protocol

**Broker:** Mosquitto 2.1.2, MQTT 5.0 required.

---

## 1. Topic Format

```
ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
```

All ESB topics are exactly 6 segments. `{msgType}` is always lowercase.

| Segment | Values | Notes |
|---|---|---|
| `ESB` | fixed | Namespace prefix |
| `participantType` | `Client` \| `Pfs` \| `Ds` \| *(user-defined)* | Game-side types are fixed; external participants define their own string |
| `connectionId` | e.g. `g2w2` | 4-char base-36, stable per participant-type per machine |
| `scope` | see Section 3 | Logical domain; open-ended string |
| `msgType` | `req` \| `res` \| `evt` \| `log` | Always lowercase |
| `operation` | PascalCase | May carry a dot-suffix meta-operation, e.g. `GetInfo.Describe` |

The **dispatch key** used for handler routing is `{scope}/{msgType}/{operation}` (base operation only, no dot-suffix; the meta-operation is available separately on `ParsedTopic`).

---

## 2. Participants

| Type | Description |
|---|---|
| `Client` | Game client (player session) |
| `Pfs` | PlayfieldServer process |
| `Ds` | DedicatedServer process |
| *(user-defined)* | Any external tool or agent; uses its own type string |

---

## 3. Scopes

| Scope | Description |
|---|---|
| `App` | Application-level: startup/shutdown, diagnostics, game ticks |
| `Playfield` | Playfield load/unload and state |
| `Entity` | Entity load/unload within a playfield |
| `Chat` | In-game chat messages |
| `Player` | Player state: health, credits, inventory, teleport |
| `Structure` | Structures: fuel, tanks, signals, docked vessels |
| `Device` | Structure-mounted devices: LCDs, lights, containers, teleporters |
| `Registry` | Participant registration events from Clients |

---

## 4. Message Types

| msgType | Description |
|---|---|
| `req` | Request; carries MQTT5 `ResponseTopic` and `CorrelationData` properties |
| `res` | Response; echoes `CorrelationData` from the matched request |
| `evt` | Server-pushed event; no correlation |
| `log` | Diagnostic output; payload is a JSON string, no fixed schema |

There is no `err` message type. Timeout and error handling are the caller's responsibility.

---

## 5. Request / Response

A participant publishes a `req` to its own topic and sets the MQTT5 `ResponseTopic` to its matching `res` topic. Any participant with a registered handler for the dispatch key receives the request and replies to the `ResponseTopic`, echoing `CorrelationData`.

**Subscriptions established at startup:**

```
ESB/{myType}/{myId}/+/res/+     -- responses directed to this participant (set at connect)
ESB/+/+/+/req/+                 -- all inbound requests from any participant
ESB/Client/+/Registry/evt/#     -- Client registry events (Pfs and Client only)
```

**Example: player info request**

```
pub  ESB/Pfs/g2w2/Player/req/GetInfo
     ResponseTopic:   ESB/Pfs/g2w2/Player/res/GetInfo
     CorrelationData: a1b2c3d4
     payload:         {"EntityId": 7}

sub  ESB/Pfs/g2w2/Player/res/GetInfo
     CorrelationData: a1b2c3d4
     payload:         {"Name": "imlarry", "Health": 100, ...}
```

`CorrelationData` is an 8-char hex fragment generated per request. In-flight requests are tracked in a pending-response map keyed by this value.

MQTT5 **User Properties** are supported on `SendAsync` for point-to-point targeting when a request must reach a specific participant.

---

## 6. Events

Events are server-pushed and carry no correlation. Any subscriber may listen.

```
ESB/Pfs/g2w2/Playfield/evt/Loaded
ESB/Pfs/g2w2/Entity/evt/EntityLoaded
ESB/Pfs/g2w2/Entity/evt/EntityUnloaded
ESB/Pfs/g2w2/Chat/evt/ChatMessageSent
ESB/Client/9baq/App/evt/GameEnter
```

Wildcard examples:

```
ESB/Pfs/+/Entity/evt/#      -- all entity events across all playfields
ESB/+/+/App/evt/#           -- application events from any participant
```

---

## 7. Registry

At connect each participant publishes a retained entry so late-joining subscribers receive current bus membership immediately.

```
ESB/Registry/{connectionId}
payload: {"type": "Pfs"}
```

An empty retained payload clears the entry at clean disconnect. An MQTT5 Will message (set at connect) does the same on unexpected disconnect.

---

## 8. Logging

ESB participants publish diagnostics to their own `App/log/{operation}` topic. Payload is a JSON string; no fixed schema.

```
ESB/Pfs/g2w2/App/log/ConnectAsync     -- connection established
ESB/Pfs/g2w2/App/log/Subscribed       -- subscription confirmed
ESB/Pfs/g2w2/App/log/UpdateHandler    -- exception caught in drain loop
```

---

## 9. Entity Types

Entity type values appear in `EntityLoaded` and `EntityUnloaded` event payloads.

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

**Logical groupings:**

| Group | Members |
|---|---|
| Structures | `BA`, `CV`, `SV`, `HV` |
| Player | `Player` |
| Player Belongings | `PlayerBackpack`, `PlayerBike`, `PlayerBikeFolded`, `PlayerDrone`, `EscapePod`, `TerrainPlaceable` |
| NPCs | `Animal`, `Civilian`, `Trader`, `Cyborg`, `NPCFighter`, `TroopTransport` |
| Hostile Devices | `EnemyDrone`, `Turret`, `ExplosiveDevice` |
| Loot and Containers | `Item`, `DropContainer`, `MissionContainer` |
| World / Resources | `Asteroid`, `AstRes`, `AstVoxel`, `AstRingPlanet`, `UnderRes` |
| System / Meta | `Unknown`, `Proxy` |
