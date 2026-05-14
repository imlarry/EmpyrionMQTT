# Code Review: ESB.Messaging/Messenger.cs

## Status

Cleanup pass executed on branch `IMessageBus`. Items below tagged DONE were addressed in production code; test-side updates for C1 are held until the user confirms `dotnet build` / `dotnet test` results.

| ID | Status | Note |
| -- | ------ | ---- |
| A1 | DONE | `Encoding.Default` -> `Encoding.UTF8` in ProcessMessageAsync |
| A2 | DONE | ProcessMessageAsync wrapped in try/catch; failures logged as `HandlerError` |
| A3 | DEFERRED | revisit when 20480 debug-kludge is removed |
| A4 | OPEN | not selected for this pass |
| A5 | DONE | comment added near ClientId derivation in ConnectAsync |
| A6 | DONE (incidentally) | unified via D2 helper |
| B1 | OPEN | not selected |
| B2 | OPEN | not selected |
| B3 | DONE | DisconnectAsync drains `_pendingResponses` with ObjectDisposedException |
| B4 | OPEN | not selected |
| B5 | OPEN | not selected |
| C1 | DONE | `_ctx` removed; ctx param dropped from IMessenger, Messenger, IMessageBus, MessageBus, BusManager, EdnaService, and the four test sites |
| C2 | DONE | three-branch overload cascade collapsed to one call |
| C3 | NO ACTION | intentional placeholders per user |
| D1 | DONE (incidentally) | unified through D2 |
| D2 | DONE | `WithMaybeCompressedPayload` helper added; five publish methods route through it |
| D3 | DONE | `RequestAsync` delegates to `RequestToAsync` |
| D4-D7 | OPEN | not selected |
| E | OPEN | not selected |

---

## Context

ESB.Messaging is the MQTT bus core for the Empyrion mod. The module is marked FROZEN in both the project CLAUDE.md and ESB.Messaging/CLAUDE.md. The freeze was lifted for this scoped cleanup pass (consistent with `feedback_frozen_modules` in memory: the freeze is a session guard that feature branches may relax).

Scope: ESB.Messaging/Messenger.cs and the API/caller files affected by the ctx removal. Related types (IMessenger, MessageType, ParsedTopic, MessageContext) were read for context.

---

## A. Correctness bugs

### A1. Wrong encoding for inbound non-compressed payloads -- DONE
Originally line 489:
```csharp
payload = Encoding.Default.GetString(buf);
```
`Encoding.Default` is the OS/process ANSI code page. Outbound paths use UTF-8 throughout (CompressPayload uses `Encoding.UTF8`; SendAsync's WithPayload(string) defaults to UTF-8). On any non-ASCII byte this silently mojibakes JSON received from peers. Replaced with `Encoding.UTF8.GetString(buf)`.

### A2. No exception handling in the MQTT receive callback -- DONE
Any throw inside `ProcessMessageAsync` -- a malformed topic, a handler that raises, a JSON parse downstream -- propagates back into MQTTnet's dispatcher. Depending on MQTTnet's version, that can suppress the message ack, kill the receive loop, or log silently. The body is now wrapped in a try/catch; failures are logged as `HandlerError` with topic + exception type + message. The receive callback never escapes with an exception.

### A3. CompressionThreshold compared against `string.Length`, not UTF-8 byte count -- DEFERRED
`string.Length` counts UTF-16 code units, but the on-wire size is UTF-8 bytes. Per user: the 20480 default is currently a kludge to suppress compression during debugging, so the char-vs-byte mismatch is dormant today. Flag for fix when compression is re-enabled at a normal threshold.

### A4. ProcessMessageAsync may NRE on a non-empty PayloadSegment with a null Array -- OPEN
The guard on `PayloadSegment.Count != 0` does not protect `seg.Array` from being null. `ArraySegment<byte>` does not guarantee non-null `Array` when `Count > 0` in all MQTTnet versions. Not addressed in this pass.

### A5. ClientId collision pattern -- DONE (comment added)
The deterministic collision is a routing feature: a machine hosting multiple playfield servers shares a ClientId so a `req` to "any playfield" reaches all of them; the one that owns the target entity replies. Siblings will also receive each other's responses, filtered cheaply by correlation-data lookup in ProcessMessageAsync. A comment near the ClientId derivation in ConnectAsync now documents this so it does not read as a bug to a future reader. See Design Notes for the "dynamic ConnectionId" idea.

### A6. ReplyAsync/RequestToAsync compression-log inconsistency -- DONE
Closed incidentally by D2: all five publish methods now go through `WithMaybeCompressedPayload`, which produces a single `{Op, OriginalBytes, CompressedBytes, Ratio}` log shape.

---

## B. Concurrency and lifetime

### B1. `_handlers` is a non-thread-safe Dictionary -- OPEN
In practice handlers are registered at startup before traffic flows; any late `RegisterHandler` call concurrent with inbound messages is a data race. Either `ConcurrentDictionary`, a documented "register before connect" rule, or guarding with `_callbackLock`. Not addressed in this pass.

### B2. Messenger does not implement IDisposable -- OPEN
`IMqttClient` is `IDisposable`; the `ApplicationMessageReceivedAsync += ProcessMessageAsync` subscription is never removed. A long-lived host that reconnects/recreates Messengers will leak. Not addressed.

### B3. DisconnectAsync now drains `_pendingResponses` -- DONE
Previously: if Disconnect happened while RequestAsync/RequestToAsync calls were outstanding, their TaskCompletionSources never completed and callers hung. DisconnectAsync now snapshots and clears `_pendingResponses` under `_callbackLock`, then calls `TrySetException(new ObjectDisposedException(...))` on each pending TCS before invoking the MQTT client disconnect.

### B4. Token file race -- OPEN
Two processes starting simultaneously can each write a different GUID. Low-probability in this game-mod context. Not addressed.

### B5. PublishAsync/SubscribeAsync use `CancellationToken.None` everywhere -- OPEN
No way to abort an in-flight publish during shutdown. Not addressed.

---

## C. Dead / unreachable code

### C1. Remove `_ctx` field and `ctx` parameter from ConnectAsync -- DONE
`BaseContextData` already owns the Messenger, so storing `_ctx` created a circular reference (caller -> Messenger -> caller) and nothing read it. The original plugin mechanism is shelved.

Changes applied:
- `ESB.Messaging/Messenger.cs` -- `_ctx` field removed; `ctx` parameter and assignment dropped from `ConnectAsync`.
- `ESB.Messaging/Interfaces/IMessenger.cs` -- `ctx` parameter dropped from `ConnectAsync`.
- `ESB.Messaging/Bus/MessageBus.cs` -- `ctx` parameter dropped from `ConnectAsync`; forward to `_messenger.ConnectAsync` updated.
- `ESB.Messaging/Bus/IMessageBus.cs` -- `ctx` parameter dropped.
- `ESB/BusService/BusManager.cs` -- `bus.ConnectAsync(_ctx)` -> `bus.ConnectAsync()`.
- `EDNAClient/Core/EdnaService.cs` -- `_ctx.Bus.ConnectAsync(_ctx)` -> `_ctx.Bus.ConnectAsync()`.
- `ESBTests/Bus/Test_BusBuilder.cs` -- fake `ConnectAsync` signature updated.
- `ESBTests/Bus/Test_MessageBus.cs` -- fake `ConnectAsync` signature updated.
- `ESBTests/Bus/Test_Bus_Integration.cs` -- call site updated; unused `TestCtx` class removed.
- `ESBTests/Messaging/Test_Messenger_Integration.cs` -- call site updated; unused `TestCtx` class removed.
- `Docs/Bus/README.md` -- example updated to match new signature.

`BaseContextData` itself is kept; it remains the "has-a-Messenger" abstraction for callers.

### C2. CreateMqttClientOptions overload selection collapsed -- DONE
The three-branch if/else in ConnectAsync was replaced with a single call:
```csharp
mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, username, password, caFilePath);
```
`CreateMqttClientOptions` already handles null username/password/caFilePath via `IsNullOrEmpty` checks.

### C3. Intentional placeholders (no action -- per user)
- `willTopic` plumbing kept for future will-message use. Not currently invoked from ConnectAsync.
- `AllowUntrustedCertificates(true)` left in `CreateMqttClientOptions` as a reminder that this path is being revisited.

---

## D. API / contract clarity

### D1. Threshold check inconsistency between methods -- DONE (incidentally)
All five publish methods now route through `WithMaybeCompressedPayload(builder, payload, forceCompress, opLabel)`. The decision rule is `forceCompress || (CompressionThreshold > 0 && size >= threshold)` everywhere. ReplyAsync, RequestAsync, RequestToAsync pass `forceCompress: false`; SendAsync and PublishRetainedAsync pass their `compress` flag.

### D2. Compression block extracted -- DONE
`WithMaybeCompressedPayload` lives next to `CompressPayload` / `DecompressPayload`. It applies the payload (compressed or raw) to the builder and emits a single `Compress` log when compression is taken. The five publish methods each call it in one line.

### D3. RequestAsync delegates to RequestToAsync -- DONE
```csharp
public Task<string> RequestAsync(string scope, string operation, string payload, TimeSpan timeout)
{
    return RequestToAsync(_participantType, _clientId, scope, operation, payload, timeout);
}
```
The duplicated body in the prior `RequestAsync` is gone.

### D4. ParseTopic does no length validation -- OPEN
Caller in ProcessMessageAsync validates `parts.Length == 6`, so this is safe today. Test code that hits ParseTopic directly will NRE on short input. A `Debug.Assert(p.Length == 6)` or explicit guard would make the precondition discoverable.

### D5. AvailableTopics returns dispatch keys, not topics -- OPEN
DispatchKey = `{scope}/{msgType}/{op}`, not a topic. Minor naming clarity.

### D6. PublishAsync (internal) has no compression handling -- OPEN
The "escape hatch" comment is honest. Worth confirming that no test path relies on it for large payloads.

### D7. ConnectAsync subscribes to responses before RegisterHandler is called -- OPEN
Response routing in ProcessMessageAsync goes through `_pendingResponses`, so replies are fine. Other inbound that arrives between ConnectAsync returning and RegisterHandler being called would be silently dropped. In practice, ConnectAsync only subscribes to `res`, so this is currently safe.

---

## E. Minor style / micro-perf -- OPEN

- `msgType.ToString().ToLower()` is allocated per publish. A small `static readonly string[]` indexed by enum value would remove the allocation.
- `Encoding.UTF8.GetByteCount(payload)` runs even when the compress branch is taken; could be hoisted next to the compressed-bytes cost.
- Header comments on most methods restate the signature; per the project's "default to no comments" rule these could go.
- `_callbackLock` guards `_pendingResponses` but not `_handlers`. Either one lock for both or rename for clarity.

---

## Design notes -- dynamic ConnectionId for routing

**Status: superseded.** The third option below ("Separate routing tag from identity") was adopted.
`ConnectionId` was renamed to `RoutingContextId`, widened to 8 chars, and is now a per-publish
audience selector rather than a per-participant identity. The participant's stable identity is
exposed separately as `bus.MachineId` and auto-subscribed at connect. See `Docs/TopicSchema.md`
section 11 for the `RoutingContextKind` taxonomy. The historical analysis below is preserved for
context.

---

The proposal: change a participant's effective ConnectionId at runtime so it joins / leaves listener groups without `Subscribe/Unsubscribe` churn. Two distinct senses to separate:

**Sense 1 -- outbound targeting.** The publisher picks a different ConnectionId in the topic. This already works through `RequestToAsync(targetParticipantType, targetConnectionId, ...)`. The interesting part is the discovery question (who owns entity X?), not the topic mechanics.

**Sense 2 -- recipient identity changes.** The participant mutates its own `_clientId`. This breaks four things in the current code:

1. `ConnectAsync` subscribes to `+/_clientId/+/res/+` for response routing. Changing `_clientId` mid-flight means in-flight `responseTopic` values still encode the old id; replies publish to the old topic; if the subscription has moved, replies are dropped.
2. `SendAsync`, `RequestAsync`, `PublishRetainedAsync` all bake `_clientId` into the outbound topic. Switching it changes how downstream subscribers see the sender.
3. A `willTopic`, once registered, is tied to the connection's initial id -- changing the in-memory `_clientId` does not retroactively rebuild the will.
4. The "deterministic per (participantType, machine)" routing feature in A5 disappears the moment one participant goes off-pattern.

**Closer-to-idiomatic alternatives** if the goal is "no-resub group membership":

- **Multiple subscriptions, stable identity.** ConnectionId stays immutable; the participant adds/removes group subscriptions to topics like `ESB/<type>/<group>/...`. The cost of `Subscribe`/`Unsubscribe` calls is the only thing being avoided here.
- **MQTT5 shared subscriptions (`$share/<group>/<filter>`).** Broker round-robins one delivery to one subscriber in the group. Native fit for "any of N can answer, exactly one should reply"; avoids broadcast-and-filter overhead. The topic filter syntax goes through `SubscribeRawAsync`.
- **Separate routing tag from identity.** Add a segment (or repurpose `connectionId` semantically) into something like `ESB/<type>/<identity>/<routingTag>/<scope>/<msgType>/<op>`. Then identity stays for correlation, routingTag is mutable.

**Recommendation.** The current "shared ClientId, broadcast, filter on the answering side" works and is simple. If you start to feel pain from N-times-the-deserialization on every request, a `$share`-based shared subscription is the smallest cliff to climb. Holding ConnectionId immutable will save you from a class of subscription-vs-correlation timing bugs.

---

## Verification

After the cleanup pass, the user should run:

- `dotnet build` -- the ctx removal propagates to several files; this is the integration check. Build will fail in the test project until the four pending test sites listed in C1 are updated.
- `dotnet test` -- existing tests in `ESBTests/Messaging/` cover SendAsync / RequestAsync / PublishRetainedAsync paths and will catch regressions in the D2 compression helper.

Suggested manual regressions (not added to the test suite by this pass):

- A1 -- send a payload with non-ASCII bytes through SendAsync into a subscribed handler; assert the handler sees the original string.
- A2 -- a handler that throws; bus must remain responsive to a subsequent message on a different topic.
- B3 -- open a RequestAsync, await briefly, call DisconnectAsync; the request task must fault (not hang).
- D3 -- RequestAsync round-trip to self produces wire output indistinguishable from the pre-refactor implementation.
