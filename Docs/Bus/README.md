# IMessageBus -- Developer Guide

`IMessageBus` is the high-level messaging API built on top of `IMessenger`. It hides MQTT topics,
dispatch keys, correlation IDs, and JSON extraction behind a typed, convention-driven surface.
A developer only needs to know: scope, operation, and payload type.

---

## Contents

1. [Setup](#1-setup)
2. [Typed Handlers](#2-typed-handlers)
3. [Lambda Handlers](#3-lambda-handlers)
4. [Publishing Events](#4-publishing-events)
5. [Making Requests](#5-making-requests)
6. [Retained Announcements](#6-retained-announcements)
7. [Error Handling](#7-error-handling)
8. [Scopes](#8-scopes)
9. [Identity and Diagnostics](#9-identity-and-diagnostics)

---

## 1. Setup

Configure and build the bus with `BusBuilder` before calling `ConnectAsync`.
All handler registration must happen before (or in place of) `ConnectAsync`.

```csharp
var messenger = new Messenger();

IMessageBus bus = new BusBuilder()
    .WithMessenger(messenger)
    .WithParticipantType("Client")
    .WithConnection("localhost", 1883)
    .WithServiceProvider(serviceProvider)   // optional; enables DI for handler classes
    .ScanAssembly(typeof(MyHandlers).Assembly)
    .Build();

await bus.ConnectAsync(ctx);
```

`Build()` registers all handlers with the underlying messenger.
`ConnectAsync(ctx)` connects to the broker and activates all subscriptions.

**BusBuilder options**

| Method | Description |
|---|---|
| `WithMessenger(IMessenger)` | Required. The configured Messenger instance. |
| `WithParticipantType(string)` | Required. Participant type token (e.g. "Client", "Pfs"). |
| `WithConnection(host, port)` | Broker address. Default: localhost:1883. |
| `WithCredentials(user, pass)` | Optional broker credentials. |
| `WithCertificate(caFilePath)` | Optional TLS certificate path. Switches default port to 8883. |
| `WithCompressionThreshold(int)` | Override compression threshold (bytes). Default: 2048. |
| `WithServiceProvider(IServiceProvider)` | DI container for resolving handler instances. |
| `ScanAssembly(Assembly)` | Discover all `[BusRoute]`-decorated handler classes. |
| `AddHandler(Type)` | Register a single handler type explicitly. |

---

## 2. Typed Handlers

Typed handlers are the primary pattern. A handler class declares its scope and operation via
`[BusRoute]` and implements either `IEventHandler<T>` or `IRequestHandler<TReq, TRes>`.
Dependencies are injected through the constructor when a DI provider is configured.

### Event handler

```csharp
[BusRoute("Player", "GameEnter")]
public class PlayerGameEnterHandler : IEventHandler<GameEnterPayload>
{
    private readonly ILogger _log;

    public PlayerGameEnterHandler(ILogger log) { _log = log; }

    public Task HandleAsync(MessageEnvelope<GameEnterPayload> envelope)
    {
        _log.Info("Player entered: " + envelope.Body.EntityId);
        return Task.CompletedTask;
    }
}
```

### Request handler

```csharp
[BusRoute("Player", "GetInfo")]
public class PlayerGetInfoHandler : IRequestHandler<PlayerInfoRequest, PlayerInfoResponse>
{
    private readonly IPlayerService _players;

    public PlayerGetInfoHandler(IPlayerService players) { _players = players; }

    public async Task<PlayerInfoResponse> HandleAsync(
        MessageEnvelope<PlayerInfoRequest> envelope)
    {
        return await _players.GetInfo(envelope.Body.EntityId);
    }
}
```

### Registration

Use `ScanAssembly` to auto-discover all decorated handler classes in an assembly:

```csharp
builder.ScanAssembly(typeof(PlayerGameEnterHandler).Assembly);
```

Or register a single type explicitly:

```csharp
builder.AddHandler(typeof(PlayerGetInfoHandler));
```

`[BusRoute]` is required on any type registered via `AddHandler` or discovered by `ScanAssembly`.
Types without the attribute are silently skipped during scanning.

### DI resolution

When `WithServiceProvider` is configured, each handler instance is resolved from the container
per invocation. If the container returns null (type not registered), `Activator.CreateInstance`
is used as a fallback, which requires a public parameterless constructor.

---

## 3. Lambda Handlers

Lambda handlers are registered on `BusBuilder` for inline or closure-based handling,
or directly on `IMessageBus` for dynamic registration after connect.

```csharp
// On builder (before connect)
builder.OnEvent<GameEnterPayload>("Player", "GameEnter", async env =>
{
    Console.WriteLine("Player entered: " + env.Body.EntityId);
});

builder.OnRequest<PlayerInfoRequest, PlayerInfoResponse>("Player", "GetInfo", async env =>
{
    return new PlayerInfoResponse { Name = "imlarry", Health = 100 };
});

// On bus (after connect -- subscription is set up immediately)
bus.OnEvent("Structure", "FuelChanged", async env =>
{
    var level = (float)env.PayloadJson["FuelLevel"];
    Console.WriteLine("Fuel: " + level);
});
```

### Untyped fallback

All `OnEvent` and `OnRequest` methods have untyped overloads that receive a plain
`MessageEnvelope`. Use these for cross-cutting concerns or when you do not have a payload model.

```csharp
bus.OnEvent("App", "GameEnter", async env =>
{
    Console.WriteLine(env.RawPayload);
});

bus.OnRequest("Player", "GetInfo", async env =>
{
    return "{ \"Name\": \"imlarry\" }";  // caller returns pre-serialized JSON
});
```

---

## 4. Publishing Events

```csharp
await bus.PublishEventAsync("Player", "PositionUpdate", new
{
    EntityId = 7,
    X = 100.0f,
    Y = 50.0f
});
```

The payload is serialized to JSON automatically. The scope's first character is normalized to
uppercase, so `"player"` and `"Player"` are equivalent.

`BusScope` enum overloads are available as extension methods:

```csharp
await bus.PublishEventAsync(BusScope.Player, "PositionUpdate", payload);
```

---

## 5. Making Requests

```csharp
var response = await bus.RequestAsync<PlayerInfoRequest, PlayerInfoResponse>(
    scope:     "Player",
    operation: "GetInfo",
    payload:   new PlayerInfoRequest { EntityId = 7 },
    timeout:   TimeSpan.FromSeconds(5));

Console.WriteLine(response.Body.Name);
```

When you do not have a typed response model, use the untyped overload and inspect the envelope:

```csharp
var response = await bus.RequestAsync<PlayerInfoRequest>(
    "Player", "GetInfo", new PlayerInfoRequest { EntityId = 7 },
    TimeSpan.FromSeconds(5));

var name = (string)response.PayloadJson["Name"];
```

---

## 6. Retained Announcements

`AnnounceAsync` publishes a retained message so late-joining subscribers receive current state
immediately on connect. This is how Registry-style presence works.

```csharp
await bus.AnnounceAsync("Registry", "Connect", new
{
    Type = "Client",
    ConnectionId = bus.ConnectionId
});
```

The retained entry is automatically cleared when the participant disconnects cleanly (the underlying
Messenger publishes an empty payload on disconnect via MQTT Will).

An optional expiry can be set:

```csharp
await bus.AnnounceAsync(BusScope.Registry, "Connect", payload, expirySeconds: 3600u);
```

---

## 7. Error Handling

### Request timeout

If no response arrives within the timeout period, `RequestAsync` throws `TimeoutException`.
This is the caller's responsibility to handle -- there is no `err` message type in the protocol.

### Bus error convention

When the responding handler returns a payload containing a top-level `"error"` string field,
the bus throws `BusRequestException`:

```csharp
// Responding side returns this payload:
// { "error": "EntityId not found" }

// Requesting side:
try
{
    var response = await bus.RequestAsync<PlayerInfoRequest, PlayerInfoResponse>(
        "Player", "GetInfo", request, TimeSpan.FromSeconds(5));
}
catch (BusRequestException ex)
{
    Console.WriteLine("Request failed: " + ex.BusError);
}
```

Non-conforming payloads (no `"error"` field, non-JSON, empty) pass through without throwing.
This convention is opt-in -- handlers that do not adopt it simply reply with their normal payload.

### Envelope access

`MessageEnvelope` exposes the raw response for inspection when the error convention is not used:

| Member | Description |
|---|---|
| `RawPayload` | Raw JSON string as received |
| `PayloadJson` | Lazily parsed `JObject`; null if payload is empty or not valid JSON |
| `PayloadAs<T>()` | Deserializes the payload to T via Newtonsoft.Json |
| `Body` | Typed shorthand on `MessageEnvelope<T>` -- equivalent to `PayloadAs<T>()` evaluated once at construction |
| `Scope` | Scope segment from the inbound topic |
| `Operation` | Operation name (base, no dot-suffix) |
| `MsgType` | Message type string: evt, req, res, log |
| `SenderType` | Participant type of the sender |
| `SenderConnectionId` | Connection ID of the sender |
| `CorrelationId` | Correlation hex string; empty on response envelopes from RequestAsync |

---

## 8. Scopes

The `BusScope` enum covers the scopes defined in the topic schema:

| Enum value | Topic segment | Description |
|---|---|---|
| `App` | `App` | Application-level: startup, shutdown, diagnostics |
| `Playfield` | `Playfield` | Playfield load/unload and state |
| `Entity` | `Entity` | Entity load/unload within a playfield |
| `Chat` | `Chat` | In-game chat messages |
| `Player` | `Player` | Player state: health, credits, inventory |
| `Structure` | `Structure` | Structures: fuel, tanks, signals, docked vessels |
| `Device` | `Device` | Structure-mounted devices |
| `Registry` | `Registry` | Participant presence events |

All `IMessageBus` methods accept either `BusScope` (via extension methods) or a plain `string`.
Custom scopes not listed above are supported by passing a string directly.
The first character is normalized to uppercase; `"player"` and `"Player"` are equivalent.

---

## 9. Identity and Diagnostics

```csharp
Console.WriteLine(bus.ParticipantType);   // e.g. "Client"
Console.WriteLine(bus.ConnectionId);      // e.g. "g2w2"  (4-char base-36, stable per machine)
Console.WriteLine(bus.AvailableTopics()); // CSV of registered dispatch keys
```

`ConnectionId` is stable per participant type per machine. It is derived from a persistent token
and the participant type string, so the same machine always connects with the same ID for a given
type. See `Docs/TopicSchema.md` section 1 for the full topic format.
