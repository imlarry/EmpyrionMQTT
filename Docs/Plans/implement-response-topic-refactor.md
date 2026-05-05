# Implement Response Topic Refactor

## Goal

Replace ad-hoc routing-in-topic patterns with a stable, deterministic ESB/ topic schema where
every segment has a single fixed meaning, and MQTT 5 metadata handles response routing and
correlation.

---

## Topic Schema

```
ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
```

| Segment         | Meaning                                      | Casing                         |
|-----------------|----------------------------------------------|--------------------------------|
| ESB             | Namespace prefix                             | Always uppercase               |
| participantType | Publisher or request initiator identity type | As passed to ConnectAsync      |
| connectionId    | 4-char base-36 hash of (participantType+token) | Always lowercase             |
| scope           | Domain area (App, Game, Tracking, ...)       | As passed by caller            |
| msgType         | Message type                                 | Always lowercase (see below)   |
| operation       | Action or intent name                        | As passed by caller            |

### msgType values

| Value | Meaning                                                    |
|-------|------------------------------------------------------------|
| evt   | Event published by participantType/connectionId            |
| req   | Request initiated by participantType/connectionId          |
| res   | Response to a req; published to the ResponseTopic header   |
| log   | Diagnostic log from participantType/connectionId           |

Errors are returned inside the payload of a res message. There is no separate error msgType.

### DispatchKey

Handlers are registered by dispatch key: `{scope}/{msgType}/{operation}`

Examples:
```
Tracking/req/Enable
Tracking/res/Enable
Game/evt/GameEntered
App/log/ConnectAsync
```

---

## Examples

```
ESB/Client/ad4x/Game/evt/GameEntered       client enters a game
ESB/Edna/p32r/Tracking/req/Enable          Edna requests the client enable tracking
ESB/Edna/p32r/Tracking/res/Enable          client confirms or errors via ResponseTopic
ESB/Client/ad4x/Tracking/evt/Ping          client sends tracking data (may include user properties to target Edna/p32r)
```

---

## Request / Response Flow

1. Requester calls `RequestAsync(scope, operation, payload, timeout)`.
2. A correlation shortId (8-char GUID fragment) is generated and stored as a pending TCS.
3. The req message is published with:
   - Topic: `ESB/{myType}/{myId}/{scope}/req/{operation}`
   - `ResponseTopic`: `ESB/{myType}/{myId}/{scope}/res/{operation}`
   - `CorrelationData`: ASCII bytes of shortId
4. The responder receives the req, processes it, and calls `ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, payload)`.
5. The requester receives the res on its persistent response subscription (`ESB/{myType}/{myId}/+/res/+`),
   matches by correlationData, and resolves the TCS.

The topic always identifies the initiator of the req/res pair. The responder publishes
the res to a topic naming the requester, not itself.

---

## Point-to-Point Targeting with User Properties

To direct an evt or other message to a specific participant without encoding routing in the topic,
use MQTT 5 user properties:

```csharp
messenger.SendAsync("Tracking", MessageType.Evt, "Ping", payload,
    new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("target-participant-type", "Edna"),
        new KeyValuePair<string, string>("target-connection-id",    "p32r")
    });
```

Received user properties are surfaced in `MessageContext.UserProperties`. Filtering by target
is the responsibility of the receiver (or a future broker-side plugin).

---

## API Changes (Sprint 1)

### MessageType enum

```csharp
public enum MessageType { Req, Res, Evt, Log }   // Err removed
```

### ParsedTopic

`DispatchKey` is now `{scope}/{msgType}/{operation}`.
`MsgType` is always lowercase.

### MessageContext

Added `List<KeyValuePair<string, string>> UserProperties` (null if none received).

### IMessenger / Messenger

- `SendAsync(scope, msgType, operation, payload)` -- `name` param renamed to `operation`
- `SendAsync(scope, msgType, operation, payload, userProperties)` -- new overload
- `RequestAsync` no longer subscribes/unsubscribes a temporary topic per call
- Persistent response subscription established in `ConnectAsync`

---

## Verification -- COMPLETE

1. [x] `dotnet build` -- zero errors on ESB.Messaging project
2. [x] `dotnet test` -- all tests pass (unit + ESBTests.Messaging suite)
3. [x] Parsed topic for `ESB/Client/a1b2/Tracking/req/Enable` produces:
   - `MsgType = "req"`, `DispatchKey = "Tracking/req/Enable"`
4. [x] `SendAsync("App", MessageType.Log, "test", "{}")` produces topic `ESB/{type}/{id}/App/log/test`
5. [x] `MessageType.Err` is gone; `HandlerBase.Execute` updated to use `MessageType.Log`

---

## Test Checklist

Tests to add under `ESBTests\Messaging\`. Existing pattern: pure unit tests, no broker required.
Integration tests (broker required) follow the pattern in `ESBTests\TopicHandlerTests\`.

### Prerequisite

`ParseTopic` is `internal`. Add to `ESB.Messaging\Properties\AssemblyInfo.cs` (or any .cs file):

```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ESBTests")]
```

---

### Test_ParseTopic.cs -- no broker, pure unit tests

File: `ESBTests\Messaging\Test_ParseTopic.cs`

All tests call `new Messenger().ParseTopic(topic)` directly.

- [ ] `ParseTopic_Evt_AllFieldsCorrect` -- topic `ESB/Client/a1b2/Game/evt/GameEntered` produces
  `ParticipantType="Client"`, `ConnectionId="a1b2"`, `Scope="Game"`, `MsgType="evt"`, `Operation="GameEntered"`, `MetaOperation=null`
- [ ] `ParseTopic_Req_DispatchKeyIncludesMsgType` -- `ESB/Edna/p32r/Tracking/req/Enable` produces
  `DispatchKey="Tracking/req/Enable"`
- [ ] `ParseTopic_Res_DispatchKeyIncludesMsgType` -- `ESB/Edna/p32r/Tracking/res/Enable` produces
  `DispatchKey="Tracking/res/Enable"`
- [ ] `ParseTopic_Log_DispatchKeyIncludesMsgType` -- `ESB/.../App/log/ConnectAsync` produces
  `DispatchKey="App/log/ConnectAsync"`
- [ ] `ParseTopic_MsgType_IsLowercase` -- segment value `"evt"` is preserved as-is, not uppercased
- [ ] `ParseTopic_DotSuffix_MetaOperationSet` -- `ESB/Client/a1b2/App/log/GetPathFor.Describe` produces
  `Operation="GetPathFor"`, `MetaOperation="Describe"`
- [ ] `ParseTopic_DotSuffix_DispatchKeyUsesBaseOp` -- same input produces
  `DispatchKey="App/log/GetPathFor"` (not `"App/log/GetPathFor.Describe"`)
- [ ] `ParseTopic_NoDotSuffix_MetaOperationNull` -- operation without dot produces `MetaOperation=null`

---

### Test_MessageType.cs -- no broker, pure unit tests

File: `ESBTests\Messaging\Test_MessageType.cs`

- [ ] `MessageType_HasExactlyFourValues` -- `Enum.GetValues(typeof(MessageType)).Length == 4`
- [ ] `MessageType_DoesNotContainErr` -- `Enum.GetNames` does not contain `"Err"`
- [ ] `MessageType_Req_ToLowerIsReq` -- `MessageType.Req.ToString().ToLower() == "req"`
- [ ] `MessageType_Res_ToLowerIsRes` -- `MessageType.Res.ToString().ToLower() == "res"`
- [ ] `MessageType_Evt_ToLowerIsEvt` -- `MessageType.Evt.ToString().ToLower() == "evt"`
- [ ] `MessageType_Log_ToLowerIsLog` -- `MessageType.Log.ToString().ToLower() == "log"`

---

### Test_Messenger_Integration.cs -- broker required

File: `ESBTests\Messaging\Test_Messenger_Integration.cs`
Uses a pair of connected `Messenger` instances (publisher + subscriber) against a local broker.
Mark with `[Trait("Category", "Integration")]`.

**Topic construction**
- [ ] `SendAsync_TopicContainsLowercaseMsgType` -- publish via `SendAsync("Game", MessageType.Evt, "Ping", "{}")`;
  subscriber on `ESB/+/+/Game/evt/Ping` receives the message
- [ ] `SendAsync_TopicDoesNotContainUppercaseMsgType` -- same publish; no message arrives on
  `ESB/+/+/Game/Evt/Ping` (PascalCase filter gets nothing)

**User properties**
- [ ] `SendAsync_UserProperties_ArriveInMessageContext` -- publish with user properties
  `[{"target", "Edna"}]`; handler receives `MessageContext.UserProperties` containing that pair
- [ ] `SendAsync_NoUserProperties_UserPropertiesIsNull` -- publish without user properties overload;
  handler receives `MessageContext.UserProperties == null`

**Dispatch key routing**
- [ ] `RegisterHandler_ReqAndEvt_RoutedToSeparateHandlers` -- register handler for
  `"Tracking/req/Enable"` and a different handler for `"Tracking/evt/Enable"`; publish each;
  confirm only the matching handler fires for each
- [ ] `RegisterHandler_UnknownDispatchKey_NoHandlerFires` -- message for unregistered key is
  logged (no exception thrown)

**RequestAsync / response routing**
- [ ] `RequestAsync_ResponseTopicIsStructuredEsbTopic` -- capture the published req message;
  assert `ResponseTopic == "ESB/{type}/{id}/{scope}/res/{operation}"`
  (not a `tmp/...` topic)
- [ ] `RequestAsync_CorrelationDataDemuxesReply` -- two concurrent `RequestAsync` calls to the
  same scope/operation; both resolve to their respective payloads without cross-talk
- [ ] `RequestAsync_Timeout_ThrowsTimeoutException` -- no responder present;
  assert `TimeoutException` thrown after the configured timeout
- [ ] `RequestAsync_Timeout_CleansUpPendingEntry` -- after timeout, `_pendingResponses`
  no longer holds the entry (verify via a second call succeeding cleanly)
- [ ] `RequestAsync_ReplyAsync_EndToEnd` -- one Messenger calls `RequestAsync`; a second
  Messenger has a `RegisterHandler` for the req dispatch key and calls `ReplyAsync` using
  `ctx.ResponseTopic` and `ctx.CorrelationData`; first Messenger receives the payload

**ConnectAsync persistent subscription**
- [ ] `ConnectAsync_EstablishesResponseSubscription` -- after `ConnectAsync`, a message
  published to `ESB/{type}/{id}/AnyScope/res/AnyOp` with valid correlation data resolves a
  waiting `RequestAsync` without any additional subscribe call

---

## Follow-on Sprints

### Sprint 2 -- Fix solution callers

Update all projects that break due to:
- `MessageType.Err` removal
- `DispatchKey` format change (`{scope}/{op}` -> `{scope}/{msgType}/{op}`)
- msgType casing change in topics (PascalCase -> lowercase)
- `SendAsync` parameter rename (`name` -> `operation`)

### Sprint 3 -- Subscription helpers

Add helpers for common subscription patterns:
- Subscribe to all req for a given scope+operation from any participant:
  `ESB/+/+/{scope}/req/{operation}`
- Subscribe to all evt from a specific participantType:
  `ESB/{participantType}/+/+/evt/+`

### Sprint 4 -- Fan-out request pattern

Document and optionally helper-wrap the fan-out req pattern where many participants
subscribe to `ESB/+/+/{scope}/req/{operation}` but only one responds. The response
topic in the req header directs the reply back to the initiator regardless of who responds.
