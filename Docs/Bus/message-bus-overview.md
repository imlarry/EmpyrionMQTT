# Unified Message Bus Model for ESB

ESB.Messaging uses MQTT pub/sub with a fixed topic schema:

ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}

- `participantType`: actor category
- `connectionId`: instance identifier
- `scope`: namespace
- `msgType`: `evt` | `req` | `res` | `log`
- `operation`: semantic action

The dispatch key is `{scope}/{msgType}/{operation}`. `ParsedTopic` exposes `operation` as the base name and `MetaOperation` when a dot suffix is present.

## Message Envelope

A `MessageEnvelope` normalizes events, requests, responses and logs. It carries:

- `correlationId`
- `replyTo`
- `timestamp`
- `sender.participantType`
- `sender.connectionId`
- `scope`
- `msgType`
- `operation`
- `payload`

The envelope abstracts MQTT transport details and lets callers treat bus traffic as typed messages instead of raw topics.

## API Surface

`PublishEvent(scope, operation, payload)`
`Announce(operation, payload, expirySeconds = 0)`
`Request(scope, operation, payload, timeout)`
`Log(scope, operation, payload)`
`OnRequest(scope, operation, handler)`
`OnEvent(scope, operation, handler)`

These methods map directly to the topic schema and envelope model. Callers do not construct topics, manage correlation IDs, or wire reply routing.

## Request/Response Dispatch

Request handling uses one internal response subscription:

ESB/{subscriberParticipantType}/{subscriberConnectionId}/+/res/+

Responses arrive on a shared subscription and are routed by `correlationId` and `replyTo`, avoiding per-request subscription churn.

## Envelope Semantics

MQTT is pub/sub. The envelope converts pub/sub into RPC-like request/response and event dispatch by carrying routing metadata inside the message body.

- `scope/msgType/operation` drives dispatch
- `correlationId` and `replyTo` drive request/response

## Announcement Subscription

Announcements are published under `Announcements/evt/{operation}` and discovered by subscribing to the announcing participant type and the target `connectionId`.

Use a discovery request operation to resolve the announcer `connectionId` when it is not known in advance.
