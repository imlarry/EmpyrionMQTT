# EmpyrionMQTT -- Messaging Protocol

**Broker:** Mosquitto 2.1.2, MQTT 5.0 required.

---

## 1. Topic Format

```
ESB/{participantType}/{routingContextId}/{scope}/{msgType}/{operation}
```

All ESB topics are exactly 6 segments. `{msgType}` is always lowercase.

| Segment | Values | Notes |
|---|---|---|
| `ESB` | fixed | Namespace prefix |
| `participantType` | `Client` \| `Pfs` \| `Ds` \| *(user-defined)* | Game-side types are fixed; external participants define their own string |
| `routingContextId` | e.g. `g2w2k7v3` | 8-char base-36, or the `00000000` Broadcast sentinel. Names the audience the message is addressed to. |
| `scope` | see Section 3 | Logical domain; open-ended string |
| `msgType` | `req` \| `res` \| `evt` \| `log` | Always lowercase |
| `operation` | PascalCase | May carry a dot-suffix meta-operation, e.g. `GetInfo.Describe` |

The **dispatch key** used for handler routing is `{scope}/{msgType}/{operation}` (base operation only, no dot-suffix; the meta-operation is available separately on `ParsedTopic`).

The **audience filter** is the participant's set of active `RoutingContextId` subscriptions (see Section 11). A participant only receives messages whose `routingContextId` matches one of its subscribed rcIds.

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
| `Announcements` | Retained participant-presence and state announcements |

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

A participant publishes a `req` addressed to a routing context. MQTT5 `ResponseTopic` is set to a topic on the requester's own Machine rcId so the reply routes back. Any participant subscribed to the request's rcId that has a registered handler for the dispatch key receives the request and replies to `ResponseTopic`, echoing `CorrelationData`.

**Subscriptions established at connect (per participant):**

```
ESB/+/{myMachineRcId}/+/+/+    -- everything addressed to this participant's Machine rcId
ESB/+/00000000/+/+/+           -- Broadcast bus-wide traffic
```

Additional rcIds are added with `SubscribeAsync(rcId)` as lifecycle events fire (game enter, playfield load, ...).

**Example: player info request from a Client to a Pfs in game `g2w2k7v3`**

```
pub  ESB/Pfs/g2w2k7v3/Player/req/GetInfo
     ResponseTopic:   ESB/Client/x9q1m4ab/Player/res/GetInfo
     CorrelationData: a1b2c3d4
     payload:         {"EntityId": 7}

sub  ESB/Client/x9q1m4ab/Player/res/GetInfo
     CorrelationData: a1b2c3d4
     payload:         {"Name": "imlarry", "Health": 100, ...}
```

`CorrelationData` is an 8-char hex fragment generated per request. In-flight requests are tracked in a pending-response map keyed by this value.

MQTT5 **User Properties** are supported on `SendAsync` for point-to-point targeting when a request must reach a specific participant.

---

## 6. Events

Events are server-pushed and carry no correlation. Any subscriber to the event's `routingContextId` may listen.

```
ESB/Pfs/g2w2k7v3/Playfield/evt/Loaded                    -- game-scoped (subscribers to game rcId see it)
ESB/Pfs/p4m1z8wq/Playfield/evt/EntityLoaded              -- playfield-scoped
ESB/Pfs/p4m1z8wq/Playfield/evt/EntityUnloaded            -- playfield-scoped
ESB/Pfs/g2w2k7v3/Chat/evt/ChatMessageSent                -- game-scoped
ESB/Client/00000000/App/evt/GameEnter                    -- broadcast (lifecycle announcement)
```

Wildcard examples:

```
ESB/Pfs/+/Entity/evt/#      -- all entity events across all playfields
ESB/+/00000000/App/evt/#    -- application events on the Broadcast rcId
```

---

## 7. Announcements

Participants publish retained messages under their own `Announcements` scope so late-joining subscribers receive current state immediately. The connect announcement signals presence (routed to Broadcast); per-game reference data is routed to the Game rcId.

```
ESB/{type}/00000000/Announcements/evt/Connect
payload: {"Type": "Pfs"}

ESB/Client/g2w2k7v3/Announcements/evt/BlockAndItemMapping
payload: {"1": "AlienBlock", "2": "AlienConsole", ...}
```

Use `IMessageBus.AnnounceAsync(routingContextId, operation, payload, expirySeconds)` to publish. The bus fills in scope (`Announcements`), participant type, and the rcId you supplied.

---

## 8. Logging

ESB participants publish diagnostics on their own Machine rcId.

```
ESB/Pfs/g2w2k7v3/App/log/ConnectAsync     -- connection established (machine rcId here is the Pfs's own)
ESB/Pfs/g2w2k7v3/App/log/Subscribed       -- subscription confirmed
ESB/Pfs/g2w2k7v3/App/log/UpdateHandler    -- exception caught in drain loop
```

---

## 9. Tabular Arrays

For event or response payloads that carry an array of homogeneous records, prefer the **tabular encoding** over an array of objects. Repeating the same field names on every row wastes bytes -- often dramatically (the `Playfield/evt/Loaded` Entities array is ~35-40% smaller in tabular form).

**Shape:**

```json
{
  "Columns": ["Id", "Name", "Type", "Position"],
  "Rows":    [
    [1055,   "EscapePod",       "EscapePod", "(-0.34, 58.01, -2.36)"],
    [219864, "AsteroidSilicon", "AstRes",    "(724.80, 33.72, -298.47)"]
  ]
}
```

**When to use it:**

| Shape | Use |
|---|---|
| Tabular `{Columns, Rows}` | >= 3 records sharing the same field set |
| Array of objects | Small lists, or rows with varying fields |
| Dictionary `{"k1":"v1","k2":"v2"}` | Lookup maps; one of the fields is a unique key and there are only two columns (key, value). `Announcements/evt/BlockAndItemMapping` is the canonical example. |

Producers on the C# side use `MessageHelpers.Tabular(columns, rows)`. Consumers resolve column indices once from `Columns`, then index positionally into each row of `Rows`.

Current producers:

| Handler | Tabular field |
|---|---|
| `Structure/GetAllBlocks` | `Blocks` |
| `Playfield/evt/Loaded` | `Entities` |
| `Playfield/GetProperties` | `Players`, `Entities` |
| `Playfield/GetEntities` | `Entities` |
| `Playfield/GetPlayers` | `Players` |
| `Playfield/GetStructureDevices` | `Devices` |
| `App/GetProperties` | `Playfields` |
| `Player/GetProperties` | `Toolbar`, `Bag` (via `HandlerHelper.ItemStacksJson`) |
| Container events (`Structure/evt/ContainerContents` etc.) | `Items` (via `MessageHelpers.ItemStacksJson`) |

New payloads with large homogeneous arrays should follow the same shape.

---

## 10. Entity Types

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

---

## 11. Routing Contexts

`RoutingContextId` names the audience a message is addressed to. On the wire only the 8-char value is sent; the `RoutingContextKind` is an in-process aid for logging and discoverability.

| Kind | Seed | Audience |
|---|---|---|
| `Broadcast` | fixed sentinel `00000000` | Every participant subscribed to Broadcast (bus-wide) |
| `Machine` | persisted machine GUID at `%ProgramData%\EmpyrionESB\bus.token` | One participant instance |
| `Game` | `saveGamePath + machineId` | All participants in a specific game on a specific machine |
| `Playfield` | `gameId + solarSystemName + playfieldName` | A specific playfield (or fanout target for "all playfields in this game") |
| `Player` | `IPlayer.SteamId` | A specific player anywhere |
| `PlayerInGame` | `playerId + gameId` (canonical order) | A specific player while in a specific game |

**Subscription lifecycle:**

| Event | Action |
|---|---|
| `ConnectAsync` | Bus auto-subscribes Machine rcId and Broadcast rcId |
| `GameEnter` event | Subscriber adds Game rcId (`SubscribeAsync`) |
| `Playfield/Loaded` event | Subscriber adds the playfield rcId |
| `Playfield/Unloading` event | Subscriber removes the playfield rcId |
| `GameExit` event | Subscriber removes Game rcId |

**Choosing the right rcId at publish time:**

| Producer | rcId |
|---|---|
| Connect announcement | `Broadcast` |
| `GameEnter` / `GameExit` events | `Broadcast` (announces the new gameRcId in payload) |
| `Playfield/Loaded` event | Game (announces the new playfieldRcId in payload) |
| `Playfield/Unloading` event | Game |
| Per-game events (`ChatMessageSent`, `BlockAndItemMapping`, ...) | Game |
| Per-playfield events (`EntityLoaded`, `EntityUnloaded`) | Playfield |
| Internal logs (`App/log/*`) | Machine |

**Stateless fanout pattern.** When a request targets "the one that owns entity X" but the caller does not know which playfield holds it, publish under the Game rcId. All playfield participants subscribed to that game receive the request; the one with the entity replies; others ignore. No state lookup, no broker indirection.
