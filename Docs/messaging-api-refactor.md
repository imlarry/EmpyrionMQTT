# ESB.Messaging API Refactor

## Goal

After this refactor, no C# caller constructs or hardcodes an ESB/ topic string.
All topic assembly lives exclusively inside Messenger's method implementations.

---

## Public API -- IMessenger (declaration order)

| # | Signature |
|---|-----------|
| 1 | `string MachineId()` |
| 2 | `string ClientId()` |
| 3 | `string ParticipantType()` |
| 4 | `string AvailableTopics()` |
| 5 | `MqttClientOptions CreateMqttClientOptions(string withTcpServer, int port, string username, string password, string caFilePath, string willTopic)` |
| 6 | `Task ConnectAsync(BaseContextData ctx, string participantType, string withTcpServer, int port, string username, string password, string caFilePath)` |
| 7 | `Task DisconnectAsync()` |
| 8 | `void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler)` |
| 9 | `Task SubscribeBrokerAsync(string participantType, string connectionId, string scope, MessageType? msgType, string operation, Func<string, string, Task> callback)` -- all optional/nullable; null -> "+" |
| 10 | `Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback)` -- raw-filter path, kept for LuaMqttApi (Lua-supplied filters) |
| 11 | `Task UnsubscribeAsync(string participantType, string connectionId, string scope, MessageType? msgType, string operation)` -- all optional/nullable; null -> "+" |
| 12 | `Task ReplyAsync(string responseTopic, byte[] correlationData, string payload)` |
| 13 | `Task PublishRetainedAsync(string scope, MessageType msgType, string operation, string payload, uint expirySeconds)` -- expirySeconds defaults to 0 (no expiry) |
| 14 | `Task SendAsync(string scope, MessageType msgType, string operation, string payload, List<KeyValuePair<string, string>> userProperties)` -- userProperties defaults to null |
| 15 | `Task<string> RequestAsync(string scope, string operation, string payload, TimeSpan timeout)` |

`PublishAsync(string topic, string payload)` is demoted to `internal` on Messenger.

---

## What Changed and Why

### SubscribeBrokerAsync -- structured parameters

Previous signature took a raw `string topicFilter`. Callers assembled topic filter
strings manually, spreading format knowledge across the codebase.

New signature takes optional named segments. `null` for any segment produces `+`.
Pass `"#"` explicitly for a subtree wildcard on the operation segment.

Topic is assembled internally: `ESB/{pt}/{cid}/{scope}/{msgType}/{operation}`

Internal call in ConnectAsync (response subscription) is now expressed as:
`SubscribeBrokerAsync(participantType: _participantType, connectionId: _clientId, msgType: MessageType.Res)`

### SubscribeEventAsync -- retained as raw-filter exception

`SubscribeEventAsync` was folded into `SubscribeBrokerAsync` (via the optional
`callback` parameter) for all structured callers. It is kept on IMessenger only
because `LuaMqttApi` passes Lua-supplied filter strings at runtime -- the filter
is user script code, not a C# constant. The TODO comment in `LuaMqttApi.cs:56`
flags this for future restructuring.

### PublishRetainedAsync -- structured parameters, overloads collapsed

Previous signatures took a raw `string topic` (two overloads, with and without
`expirySeconds`). New signature takes scope/msgType/operation and a single
`expirySeconds` defaulting to 0 (no expiry). Topic is assembled internally using
the Messenger's own participantType and clientId -- same as SendAsync.

### SendAsync -- overloads collapsed

Two overloads (with and without userProperties) replaced by one method with
`userProperties = null` default. All 4-arg call sites compile unchanged.

### RequestAsync -- retained on IMessenger

`FloorMapper.cs` uses it as the primary mechanism for multi-step ESB round trips
(Player/GetProperties -> Structure/GlobalToStructPos -> Structure/ScanFloor x2).
Integration tests in `ESBTests/Messaging/Test_Messenger_Integration.cs` also cover it.
The method publishes a req with MQTT5 ResponseTopic + CorrelationData and blocks
until the matching res arrives or the timeout fires.

### Old-form Registry topic -- removed

BusManager previously published `ESB/Registry/{connId}` which did not follow the
current schema. Now publishes `ESB/{participantType}/{connectionId}/Registry/evt/Connect`
via `PublishRetainedAsync("Registry", MessageType.Evt, "Connect", ...)`.

The SubscriptionHandler filter that captures game-scoped Registry events was
`"ESB/Client/+/Registry/evt/#"` and is now expressed as:
`SubscribeBrokerAsync(participantType: "Client", scope: "Registry", msgType: MessageType.Evt, operation: "#")`

---

## Callsite Migration Reference

### PublishRetainedAsync

| File | Before | After |
|------|--------|-------|
| BusManager.cs | `PublishRetainedAsync("ESB/Registry/{connId}", json)` | `PublishRetainedAsync("Registry", MessageType.Evt, "Connect", json)` |
| BusManager.cs | `PublishRetainedAsync("ESB/Registry/{connId}", "")` | `PublishRetainedAsync("Registry", MessageType.Evt, "Connect", "")` |
| GameManager.cs | `PublishRetainedAsync(GameRetainedEventTopic(...), payload, 3600u)` | `PublishRetainedAsync("Registry", MessageType.Evt, "BlockAndIdtemMapping", payload, 3600u)` |

`GameRetainedEventTopic` helper removed from GameManager -- no remaining callers.

### SubscribeBrokerAsync / SubscribeEventAsync

| File | Before | After |
|------|--------|-------|
| Messenger.cs (internal) | `SubscribeBrokerAsync("ESB/{pt}/{id}/+/res/+")` | `SubscribeBrokerAsync(participantType: _participantType, connectionId: _clientId, msgType: MessageType.Res)` |
| SubscriptionHandler.cs | `SubscribeBrokerAsync("ESB/+/+/+/req/+")` | `SubscribeBrokerAsync(msgType: MessageType.Req)` |
| SubscriptionHandler.cs | `SubscribeBrokerAsync("ESB/Client/+/Registry/evt/#")` | `SubscribeBrokerAsync(participantType: "Client", scope: "Registry", msgType: MessageType.Evt, operation: "#")` |
| EdnaService.cs | `SubscribeEventAsync("ESB/+/+/App/evt/GameEnter", cb)` | `SubscribeBrokerAsync(scope: "App", msgType: MessageType.Evt, operation: "GameEnter", callback: cb)` |
| EdnaService.cs | `SubscribeEventAsync("ESB/+/+/App/evt/GameExit", cb)` | `SubscribeBrokerAsync(scope: "App", msgType: MessageType.Evt, operation: "GameExit", callback: cb)` |
| EdnaService.cs | `SubscribeEventAsync("ESB/+/+/Playfield/evt/Loaded", cb)` | `SubscribeBrokerAsync(scope: "Playfield", msgType: MessageType.Evt, operation: "Loaded", callback: cb)` |
| ThreatTracker.cs | `SubscribeEventAsync("ESB/+/+/App/evt/PlayfieldEntered", cb)` | `SubscribeBrokerAsync(scope: "App", msgType: MessageType.Evt, operation: "PlayfieldEntered", callback: cb)` |
| ThreatTracker.cs | `SubscribeEventAsync("ESB/+/+/App/evt/Feeds.Scan", cb)` | `SubscribeBrokerAsync(scope: "App", msgType: MessageType.Evt, operation: "Feeds.Scan", callback: cb)` |
| LuaMqttApi.cs | `SubscribeEventAsync(topicFilter, cb)` | unchanged (Lua exception) |
