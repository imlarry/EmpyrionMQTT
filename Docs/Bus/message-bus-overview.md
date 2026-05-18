# Unified Message Bus Model for ESB

ESB.Messaging uses MQTT pub/sub with a fixed topic schema:

ESB/{participantType}/{routingContextId}/{scope}/{msgType}/{operation}

- `participantType`: recipient type when machine-targeted; sender's own type for context (Lobby or Game) and broadcast events
- `routingContextId`: audience id; 5-char Machine, 8-char Lobby or Game, or `00000000` Broadcast sentinel
- `scope`: namespace
- `msgType`: `evt` | `req` | `res` | `log`
- `operation`: semantic action

The dispatch key is `{scope}/{msgType}/{operation}`. `ParsedTopic` exposes `operation` as the base name and `MetaOperation` when a dot suffix is present.

See `Docs/TopicSchema.md` section 11 for the `RoutingContextKind` taxonomy and per-event rcId guidance.

## Message Envelope

A `MessageEnvelope` normalizes events, requests, responses and logs. It carries:

- `CorrelationId`
- `SenderType`
- `RoutingContextId`
- `Scope`
- `MsgType`
- `Operation`
- `RawPayload`

The envelope abstracts MQTT transport details and lets callers treat bus traffic as typed messages instead of raw topics.

## API Surface

`PublishEventAsync(routingContextId, scope, operation, payload)`
`AnnounceAsync(routingContextId, operation, payload, expirySeconds = 0)`
`RequestAsync(routingContextId, scope, operation, payload, timeout)`
`LogAsync(routingContextId, scope, operation, payload)`
`SubscribeAsync(routingContextId)` / `UnsubscribeAsync(routingContextId)`
`OnRequest(scope, operation, handler)`
`OnEvent(scope, operation, handler)`

These methods map directly to the topic schema and envelope model. Callers do not construct topics, manage correlation IDs, or wire reply routing.

## Request/Response Dispatch

Request handling uses the participant's type-pinned Machine subscription, auto-installed at connect:

ESB/{myType}/{myMachineId}/#

Responses (and all other traffic addressed to this participant's type at its Machine rcId) arrive on this subscription. Responses are routed by `CorrelationData` to the matching pending request; other traffic flows through the dispatch table.

## Envelope Semantics

MQTT is pub/sub. The envelope converts pub/sub into RPC-like request/response and event dispatch by carrying routing metadata inside the message body.

- `scope/msgType/operation` drives dispatch
- `CorrelationData` (MQTT5) drives request/response
- `routingContextId` (topic segment 3) drives audience filtering

## Audience Subscriptions

Three always-on subscriptions per participant: Machine (type-pinned, own MachineId), Broadcast, and a **context-evt** sub whose rcId target swaps on game enter/exit. The context sub is never added or removed at runtime, only retargeted.

- Ds / Pfs: context = real Game rcId from startup (no lobby phase, no swap)
- Client: context = Lobby rcId on connect; swap to Game on `GameEnter`, back to Lobby on `GameExit`
- EDNA: same lifecycle as Client; derives the same Lobby rcId because it shares its bound Client's MachineId

`Announcements/evt/Connect` is published on the Broadcast rcId so any subscriber sees presence. `App/evt/GameEnter` / `GameExit` are also Broadcast and carry the new Game rcId in payload so other participants can adopt it.
