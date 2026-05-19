# Unified Message Bus Model for ESB

ESB.Messaging uses MQTT pub/sub with a fixed topic schema:

ESB/{participantType}/{routingContextId}/{scope}/{msgType}/{operation}

- `participantType`: recipient type when machine-targeted; sender's own type for context (Lobby or Game) events
- `routingContextId`: audience id; 5-char Machine or 8-char Lobby/Game
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
`PublishContextEventAsync(scope, operation, payload)`          -- targets Bus.ContextRcId
`AnnounceAsync(routingContextId, operation, payload, expirySeconds = 86400)`  -- default 24h, pass 0u for indefinite
`RequestAsync(routingContextId, scope, operation, payload, timeout)`
`LogAsync(routingContextId, scope, operation, payload)`
`SubscribeAsync(routingContextId)` / `UnsubscribeAsync(routingContextId)`
`SwitchContextAsync(newContextRcId)`                            -- atomic sub-new-then-unsub-old
`ContextRcId { get; }`                                          -- current audience for events
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

Two always-on subscriptions per participant: Machine (type-pinned, own MachineId) and a **context-evt** sub whose rcId target swaps via `Bus.SwitchContextAsync(newRcId)`. The context sub is never added or removed at runtime, only retargeted; the new audience is subscribed before the old one is dropped, so messages never fall through a gap.

- Ds / Pfs: `SwitchContextAsync(realGameRcId)` once at startup (no lobby phase, no further swaps)
- Client: `SwitchContextAsync(lobbyRcId)` at startup; swap to Game on `GameEnter`, back to Lobby on `GameExit`
- EDNA: same lifecycle as Client; derives the same Lobby rcId because it shares its bound Client's MachineId

`Announcements/evt/Connect` is published on the participant's current ContextRcId (retained, 24h default expiry). It is null-posted on Lobby <-> Game swap and on graceful disconnect via the `DisconnectCleanup` registry; ungraceful exits fall back to the 24h TTL. `App/evt/GameEnter` / `GameExit` fire on the new ContextRcId after the swap as an intra-context lifecycle hint -- participants compute the Game rcId locally from the save game path and do not depend on these events for discovery.
