# API Consolidation — Open Design Questions

This document records the open issues around consolidating the ESB MQTT topic interface. No decisions are locked in yet.

## Proposed Topic Collapse

Current topic format:

```
{appId}/{msgclass}/{subject}/{clientId}/{seq}
```

Where `appId` is `Client`, `DedicatedServer`, or `PlayfieldServer` — the process type of the ESB instance that should handle the message.

Proposed collapsed format:

```
ESB/{msgclass}/{subject}/{clientId}/{seq}
```

The process prefix is removed. ESB itself becomes responsible for routing internally.

**Motivation:** Callers (EDNA skills, Lua scripts) currently need to know the process topology to address requests correctly. Collapsing the prefix would make the topic interface fully mode-agnostic — skills always send to `ESB/Q/Player.GetInventory` and the right ESB instance responds, regardless of game mode.

This aligns with the unified API goal: V1/V2 and process-type routing are implementation details that should not leak into the public topic interface.

## Open Issues

### 1. Scope-Aware Handler Registration

Currently each ESB instance subscribes only to `{ownAppId}/Q/...` — the appId prefix *is* the routing mechanism. With a flat `ESB/Q/...`, all three ESB instances receive every request.

A replacement mechanism is needed: **handler registration must declare which process type(s) are authoritative for each subject.** On receiving `ESB/Q/Player.GetInventory`, Client and PlayfieldServer instances look up the registry, find no authoritative handler, and drop the message. DedicatedServer responds.

This requires extending the registration API, e.g.:

```csharp
new V1.Player(_cntxt).Register(ProcessScope.DedicatedServer);
new V2.Player(_cntxt).Register(ProcessScope.All);
```

### 2. Multi-Response for Shared V2 Handlers

Some handlers are valid on all process types (e.g. `Application.Mode`, `Application.GameInfo`). With a flat topic, all three instances would respond to one request, producing three responses.

Options to resolve:
- Designate a canonical source per handler (Client is authoritative for client-scoped queries; DedicatedServer for server-scoped)
- Accept multiple responses for broadcast-style queries (already the pattern for playfield fan-out)
- Restrict all V2 handlers so only one process type responds (may not always be meaningful)

### 3. Event Source Discriminator

Today `Client/E/Application.GameEnter` and `DedicatedServer/E/Application.GameEnter` are distinguishable by topic. Subscribers can wildcard `+/E/Application.GameEnter/#` to receive from all sources and inspect the prefix.

Collapsed to `ESB/E/Application.GameEnter`, the source process is invisible unless moved into the payload. Options:

- **Embed in payload:** add `"Source": "DedicatedServer"` to every event payload. Simple, but requires payload changes across all event publishers.
- **Retain source only for events:** `ESB/E/{appId}/{subject}/...` — partial collapse that preserves source discrimination for event subscribers without exposing it on the Q/R path.
- **Accept loss of source:** for most events the source process doesn't matter to subscribers; GameEnter from any process means the game entered.

### 4. Playfield Multicast

The current `PlayfieldServer/Q/+/*/#` subscription pattern fans out to all loaded PlayfieldServer instances simultaneously, enabling broadcast queries across all playfields. This is a first-class use case (e.g. find which playfield an entity is on).

With a flat `ESB/Q/...` prefix, all instances already receive all messages, so fan-out is implicit. But the caller loses the ability to target a *specific* PlayfieldServer instance by clientId routing. A replacement addressing scheme is needed for targeted vs. broadcast playfield queries.

## Relationship to the Unified API

The topic collapse and the unified handler API (V2 augmented with V1 scope extensions) are complementary but independent changes. The unified API can be built within the current topic format — callers still address `DedicatedServer/Q/Player.GetInventory` but the handler internally routes V1 vs. V2 by scope. The topic collapse is an additional step that removes the process prefix from the public interface.

Recommended sequence if both are pursued:
1. Build unified handler API (scope-aware V1/V2 routing inside ESB)
2. Evaluate topic collapse once handler-level routing is proven
