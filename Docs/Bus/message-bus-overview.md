# Unified Message Bus Model for ESB

ESB.Messaging uses MQTT pub/sub with a fixed topic schema:

ESB/{participantType}/{routingContextId}/{scope}/{msgType}/{operation}

- `participantType`: actor category
- `routingContextId`: 8-char base-36 audience id (or `00000000` Broadcast sentinel). Names the audience the message is addressed to.
- `scope`: namespace
- `msgType`: `evt` | `req` | `res` | `log`
- `operation`: semantic action

The dispatch key is `{scope}/{msgType}/{operation}`. `ParsedTopic` exposes `operation` as the base name and `MetaOperation` when a dot suffix is present.

See `Docs/TopicSchema.md` Section 11 for the `RoutingContextKind` taxonomy (Broadcast, Machine, Game, Playfield, Player, PlayerInGame) and per-event rcId guidance.

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

Request handling uses the participant's Machine rcId subscription, auto-installed at connect:

ESB/+/{myMachineRcId}/+/+/+

Responses (and all other traffic addressed to this participant's Machine rcId) arrive on this subscription. Responses are routed by `CorrelationData` to the matching pending request; other traffic flows through the dispatch table.

## Envelope Semantics

MQTT is pub/sub. The envelope converts pub/sub into RPC-like request/response and event dispatch by carrying routing metadata inside the message body.

- `scope/msgType/operation` drives dispatch
- `CorrelationData` (MQTT5) drives request/response
- `routingContextId` (topic segment 3) drives audience filtering

## Audience Subscriptions

Beyond the auto-subscribed Machine and Broadcast rcIds, participants add per-lifecycle rcIds with `SubscribeAsync`:

- on `GameEnter`: subscribe to the Game rcId from the event payload
- on `Playfield/Loaded`: subscribe to the playfield rcId from the event payload
- on `Playfield/Unloading` / `GameExit`: unsubscribe accordingly

`Announcements/evt/Connect` is published on the Broadcast rcId so any subscriber sees presence.
