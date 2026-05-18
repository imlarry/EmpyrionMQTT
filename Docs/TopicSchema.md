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
| `participantType` | `Client` \| `Pfs` \| `Ds` \| *(user-defined, e.g. `EDNA`)* | **Dual meaning** -- see below |
| `routingContextId` | `00000000` (Broadcast), 5-char Machine, or 8-char Game/Lobby | Names the audience the message is addressed to. Lobby and Game share the same width and on-the-wire shape; only the in-process `RoutingContextKind` distinguishes them. See Section 11; also see the addressing rule below. |
| `scope` | see Section 3 | Logical domain; open-ended string |
| `msgType` | `req` \| `res` \| `evt` \| `log` | Always lowercase |
| `operation` | PascalCase | May carry a dot-suffix meta-operation, e.g. `GetInfo.Describe` |

### Addressing rule

Each rcId kind has exactly one purpose. Events publish to the participant's **current context rcId** -- Lobby before game entry, Game once in a game. There is no game-targeted req/res/log; "any of type X in this game" fanout is not supported.

| Purpose | rcId | msgType |
|---|---|---|
| Request/response/log between two specific processes | **Machine** of the recipient | `req`, `res`, `log` |
| Events from a Client/EDNA before game entry (lobby fan-out) | **Lobby** | `evt` |
| Events fired during a game (in-game fan-out) | **Game** | `evt` |
| Retained game-scoped announcements (`Announcements/evt/*`) | **Game** | `evt` |
| Lifecycle / presence broadcasts (Connect, GameEnter, GameExit) | **Broadcast** | `evt` |
| Internal diagnostics from a process to itself | own **Machine** | `log` |

A process publishes to its own Machine for diagnostics, to Broadcast for presence, to its current context (Lobby or Game) for events and Announcements, and to another process's Machine for point-to-point req/res. Process-to-process req/res requires that the publisher knows the recipient's MachineId (typically learned from the recipient's Connect announcement on Broadcast).

**Lobby vs Game.** Pfs and Ds are service processes bound to a single game; they have no lobby phase and publish events directly to Game from startup. Client and EDNA start in Lobby (a per-machine context shared between the Client and its bound EDNA, since they share a MachineId), then swap context to the real Game rcId on `GameEnter` and back to Lobby on `GameExit`.

### Position 1 semantics

`{participantType}` has two meanings depending on the rcId kind:

- **Machine-targeted traffic (`req`, `res`, `log`):** position 1 is the **recipient type** -- the participant type the publisher wants to deliver to. Receivers pin their own type on the subscription (`ESB/{myType}/{myMachineId}/#`), so only the intended recipient type sees the message.
- **Game-wide events and broadcast traffic (`evt` to a gameId or `00000000`):** position 1 is the **sender's own type** (provenance). Receivers wildcard position 1 on these subs (`ESB/+/{gameId}/+/evt/+` and `ESB/+/00000000/#`), so fan-out works regardless of who published.

The dual meaning lets the same 6-segment shape carry both narrow (per-recipient) and wide (per-audience) routing without an extra header property.

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

A participant publishes a `req` addressed to the **recipient's MachineId**, with the **recipient's type** at position 1. MQTT5 `ResponseTopic` is set to a topic on the requester's own Machine rcId (with the requester's own type at position 1) so the reply routes back to that exact recipient. The recipient's type-pinned machine sub matches the request, its registered dispatch-key handler fires, and the reply is published to `ResponseTopic`, echoing `CorrelationData`.

The requester must know the recipient's MachineId before sending. MachineIds are typically learned from each participant's `Announcements/evt/Connect` retained message published to Broadcast at startup.

### Subscription patterns

Each participant maintains **three always-on subscriptions**. The third one's rcId target swaps on `GameEnter` / `GameExit`; the subscription itself is never added or removed at runtime, just retargeted.

| Subscription | Purpose | rcId target |
|---|---|---|
| `ESB/{myType}/{myMachineId}/#` | Req/res/log addressed to this participant | own MachineId (fixed) |
| `ESB/+/00000000/#` | Broadcast traffic (Connect, GameEnter, GameExit) | `00000000` (fixed) |
| `ESB/+/{contextRcId}/+/evt/+` | Events + game-scoped Announcements addressed to my current context | Lobby (Client/EDNA pre-game) or Game (everyone, in-game) |

`Pfs` and `Ds` set the context rcId to their real Game rcId at startup and never change it. `Client` and `EDNA` start with the context rcId set to the Lobby rcId, swap to the real Game rcId on `GameEnter`, and swap back to Lobby on `GameExit`. EDNA may also load a separate Game rcId into its context for offline review of saved data without an active game session.

There is no game-targeted req/res/log sub. Req/res addressing always uses MachineId.

### Publisher contract

| Publish case | Position 1 | Position 2 | Reaches |
|---|---|---|---|
| `req`/`res`/`log` to a specific process | recipient type | recipient machineId | only the recipient (via its machine sub) |
| Context `evt` (Client/EDNA pre-game lobby fan-out) | sender's own type | lobby rcId | the publisher and its sibling on the same machine (Client/EDNA share a Lobby rcId) |
| Context `evt` (in-game fan-out + game-scoped `Announcements/evt/*`) | sender's own type | gameId | every game participant (via the context evt sub) |
| Broadcast `evt` (Connect, GameEnter, ...) and broadcast `Announcements/evt/*` | sender's own type | `00000000` | every participant (via the broadcast sub) |

### Example: player info request from a Client to a specific Pfs

Client `k3m9p` (5-char machineId) requests `Player/GetInfo` from Pfs `pq8r2` (whose MachineId it learned from the Pfs's Connect announcement):

```
pub  ESB/Pfs/pq8r2/Player/req/GetInfo                     <- position 1 = recipient type (Pfs)
     ResponseTopic:   ESB/Client/k3m9p/Player/res/GetInfo  <- position 1 = sender's own type (Client)
     CorrelationData: a1b2c3d4
     payload:         {"EntityId": 7}
```

The Pfs is subscribed to `ESB/Pfs/pq8r2/#` (its type-pinned machine sub); the topic matches, the handler fires, and the reply is published to the `ResponseTopic`:

```
pub  ESB/Client/k3m9p/Player/res/GetInfo
     CorrelationData: a1b2c3d4
     payload:         {"Name": "imlarry", "Health": 100, ...}
```

The Client receives it via its `ESB/Client/k3m9p/#` machine sub.

`CorrelationData` is an 8-char hex fragment generated per request. In-flight requests are tracked in a pending-response map keyed by this value.

MQTT5 **User Properties** are supported on `SendAsync` for additional point-to-point targeting hints.

---

## 6. Events

Events are server-pushed and carry no correlation. Position 1 is the **sender's own type** (provenance); subscribers fan out via the context evt sub `ESB/+/{contextRcId}/+/evt/+` or the broadcast sub `ESB/+/00000000/#`.

```
ESB/Pfs/g2w2k7v3/Playfield/evt/Loaded         -- game-scoped (game rcId 8-char; Pfs/Ds always Game)
ESB/Pfs/g2w2k7v3/Entity/evt/EntityLoaded      -- game-scoped
ESB/Pfs/g2w2k7v3/Entity/evt/EntityUnloaded    -- game-scoped
ESB/Pfs/g2w2k7v3/Chat/evt/ChatMessageSent     -- game-scoped
ESB/Client/lby4abcd/App/evt/WindowOpened      -- lobby-scoped (Client emits this pre-game; rcId is the Client+EDNA Lobby)
ESB/Client/g2w2k7v3/App/evt/InventoryOpened   -- game-scoped (Client emits the same event in-game; rcId is the real Game)
ESB/Client/00000000/App/evt/GameEnter         -- broadcast (lifecycle announcement)
```

Wildcard examples:

```
ESB/Pfs/+/Entity/evt/#      -- all entity events from any Pfs in any game
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

ESB participants publish diagnostics on their own Machine rcId (5-char), with position 1 = own type.

```
ESB/Pfs/abc12/App/log/ConnectAsync     -- connection established (machineId here is the Pfs's own)
ESB/Pfs/abc12/App/log/Subscribed       -- subscription confirmed
ESB/Pfs/abc12/App/log/UpdateHandler    -- exception caught in drain loop
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

`RoutingContextId` names the audience a message is addressed to. Only the value is on the wire (position 2 of the topic); the `RoutingContextKind` is an in-process aid for logging and discoverability. Four kinds are defined; `Lobby` and `Game` share the 8-char width and on-the-wire shape (the kind distinguishes them in-process only).

| Kind | Width | Seed | Audience |
|---|---|---|---|
| `Broadcast` | 8 (fixed `00000000`) | -- | Every participant subscribed to Broadcast (bus-wide) |
| `Machine` | 5 base-36 chars | persisted GUID at `%ProgramData%\EmpyrionESB\bus.token`, SHA-256 truncated | One physical machine. Because one player = one Client = one machine, this is also the alternate key to a Player when paired with position 1 = `Client`. |
| `Lobby` | 8 base-36 chars | `"__lobby__" + machineId`, SHA-256 truncated | The Client + bound EDNA on one machine before game entry. Both derive the same value because they share a MachineId. |
| `Game` | 8 base-36 chars | `saveGamePath + machineId`, SHA-256 truncated | All participants in a specific game |

**Game rcId distribution.** The Ds computes the gameId at startup (or, in single-player, the Client computes it for itself). Other participants do not derive it; they receive it via the Connect or GameEnter announcement and then swap their context sub to it.

**Context rcId lifecycle:**

| Event | Action |
|---|---|
| `ConnectAsync` | Bus auto-subscribes `ESB/{myType}/{myMachineId}/#` (machine) and `ESB/+/00000000/#` (broadcast) |
| `GameManager.Init` -- Pfs / Ds | Context rcId set to real Game rcId; subscribe `ESB/+/{gameId}/+/evt/+` |
| `GameManager.Init` -- Client | Context rcId set to Lobby rcId; subscribe `ESB/+/{lobbyId}/+/evt/+` |
| `EdnaService.StartAsync` -- EDNA | Same as Client: subscribe Lobby |
| `GameEnter` (Client / EDNA) | Swap context: unsubscribe Lobby, subscribe Game |
| `GameExit` (Client / EDNA) | Swap context: unsubscribe Game, subscribe Lobby |

**Choosing the right rcId at publish time:**

| Producer | rcId | Position 1 |
|---|---|---|
| Connect announcement | `Broadcast` | sender's own type |
| `GameEnter` / `GameExit` events | `Broadcast` (carries the new gameId in payload) | sender's own type |
| Events from a Client/EDNA before game entry | `ContextRcId` (= Lobby) | sender's own type |
| Per-game events (`Entity/EntityLoaded`, `Chat/ChatMessageSent`, `Playfield/Loaded`, ...) | `ContextRcId` (= Game) | sender's own type |
| Game-scoped Announcements (`Announcements/evt/BlockAndItemMapping`, ...) | `Game` (explicit) | sender's own type |
| Point-to-point `req`/`res`/`log` | `Machine` of the recipient | recipient's type |
| Internal logs (`App/log/*`) | own `Machine` | sender's own type |

---

## 12. Subscription Loopback

The bus does not set MQTT 5's `NoLocal` subscription option, so a participant receives its own publishes on any subscription that matches them. Under the three-sub model in Section 5, loopback only occurs on the wildcard subs:

| Sub | Position 1 | Loops back own publishes? |
|---|---|---|
| `ESB/{myType}/{myMachineId}/#` | pinned to own type | No -- req/res/log put the *recipient* type at position 1, so this participant's own outbound traffic addressed to others does not match |
| `ESB/+/{contextRcId}/+/evt/+` | wildcard | Yes -- this participant's own context events (Lobby or Game) come back. For Client and EDNA on one machine, both are subscribed to the same Lobby rcId pre-game, so events also reach the sibling process. |
| `ESB/+/00000000/#` | wildcard | Yes -- this participant's own broadcasts come back |

Self-loopback is harmless: messages with no registered dispatch-key handler are silently dropped in `Messenger.ProcessMessageAsync`. The cost is one extra broker round-trip and a no-op dispatch per self-published event/broadcast.

It is kept on because it lets one process play both producer and consumer in tests, and keeps the door open for event-sourcing (single publish-and-handle mutation path), in-process bus mediation, and request/reply where the responder happens to be the requester.

Revisit if event-traffic reduction becomes a goal -- setting `WithNoLocal(true)` on the topic filter in `Messenger.SubscribeRawAsync` is a one-line change, but audit publishers first for any participant that publishes to one of its own subscribed rcIds and expects to also handle that message.

> Today's code uses a single wildcard filter `ESB/+/{myMachineId}/+/+/+` at connect rather than the type-pinned machine sub, so loopback is broader than the table above describes. The table reflects the intended end-state.
