# IMessageBus -- Next Steps

These steps describe how to move the existing system from direct IMessenger usage to IMessageBus.
They are ordered by dependency: each step builds on the previous one.

---

## Step 1: Introduce BusBuilder at each participant entry point -- DONE

BusManager.cs builds an IMessageBus via BusBuilder and stores it in _ctx.Bus.
EntityLoadedHandler.cs uses _ctx.Bus.PublishEventAsync and is the proof-of-concept event handler.

---

## Step 2: App scope proof-of-concept -- DONE

ApplicationHandler.cs replaced with AppHandler.cs. Typed POCOs created in AppPayloads.cs.
PlayerPayloads.cs created with DamageEntityRequest. Introspection (.defs.cs) removed from all
scopes. Old ApplicationHandler.defs.cs, PlayerHandler.defs.cs, StructureHandler.Defs.cs deleted.

---

## Step 3: Define payload model types -- DONE

ESB/Payloads/AppPayloads.cs: all App-scope request types plus event payloads
  (ChatMessageSentPayload, GameStatePayload, DialogResponsePayload).
ESB/Payloads/PlayerPayloads.cs: DamageEntityRequest, GetPropertiesRequest, TeleportRequest.
ESB/Payloads/EntityLoadedPayload.cs: EntityLoadedPayload and Vec3Payload (shared).
ESB/Payloads/PlayfieldPayloads.cs: EntityUnloadedPayload, PlayfieldEntitySnapshot,
  PlayfieldLoadedPayload, PlayfieldUnloadingPayload.
ESB/Payloads/StructurePayloads.cs: VecInt3Payload (shared) plus request types for all
  17 Structure operations (EntityIdRequest, GetDevicePositionsRequest, GetBlockSignalsRequest,
  GetSignalStateRequest, GetSignalReceiversRequest, GetSendSignalNameRequest,
  AddTankContentRequest, SetFactionRequest, StructToGlobalPosRequest,
  GlobalToStructPosRequest, ScanFloorRequest).

Handlers still build JObject responses inline; typed response POCOs can be added
alongside EDNA client deserialization work in Step 7.

---

## Step 4: Convert handlers to IMessageBus -- DONE

### TopicHandler conversion pattern

Each TopicHandler class follows this structure:

1. `Register()` calls `_ctx.Bus.OnRequest(scope, operation, method)` once per operation.
   The overload used is `void OnRequest(string scope, string operation,
   Func<MessageEnvelope, Task<string>> handler)`.

2. Every handler method has the signature:
   `private Task<string> MethodName(MessageEnvelope env)`

3. For operations with typed request payloads, the handler calls `env.PayloadAs<TReq>()`
   inside the method body to deserialize. The registration line is identical whether or
   not the operation has a request payload -- no wrapper or generic overload at the call site.

4. Exception handling belongs inside the handler method, not at the registration layer.
   Use `MessageHelpers.ErrorJson(message)` for expected errors and
   `MessageHelpers.ExceptionJson(ex)` for caught exceptions.

Example:
```
public void Register()
{
    _ctx.Bus.OnRequest("App", "GetPathFor", GetPathFor);
}

private Task<string> GetPathFor(MessageEnvelope env)
{
    var req = env.PayloadAs<GetPathForRequest>();
    // ... build and return JSON string
}
```

### EventHandler conversion pattern

Event publishers call `await _ctx.Bus.PublishEventAsync(scope, operation, payload)` where
payload is a JObject or a typed POCO. Exception handling belongs inside the handler, not at
the call site.

### Current status

TopicHandlers (all using Bus.OnRequest):
- AppHandler: 14 operations implemented (complete)
- PlayerHandler: 3 operations implemented (complete)
- StructureHandler: 17 operations implemented (complete)
- Registry scope retired; replaced by Announcements (publishers use Bus.AnnounceAsync)

EventHandlers (all using Bus.PublishEventAsync):
- ChatMessageSentHandler, EntityLoadedHandler, EntityUnloadedHandler,
  GameEnteredHandler, GameEventHandler, PlayfieldLoadedHandler,
  PlayfieldUnloadingHandler: converted

MainThreadRunner: RunOnMainThread<T>(Func<T>) overload added to support
synchronous main-thread work returning a value (used by App and Player handlers).

PlayfieldLoadedHandler: _ctx.GameManager.CurrentPlayfield assignment was dropped
during conversion and restored. StructureHandler.GetStructureForEntity also falls
back to _ctx.ModApi.ClientPlayfield when CurrentPlayfield is null.

SubscriptionHandler.SubscribeAll() calls Register() on each TopicHandler and returns
Task.CompletedTask. No wildcard SubscribeBrokerAsync calls remain in application code.

---

## Step 5: Implement handler bodies -- DONE (132 tests passing)

### Reference implementations in git

The prior API used IMessenger directly. Complete working implementations exist in git at
HEAD and are the primary reference for this work. Recover them with:

  git show HEAD:ESB/TopicHandlers/ApplicationHandler.cs  (all App scope handlers)
  git diff HEAD -- ESB/TopicHandlers/PlayerHandler.cs    (Properties, Teleport, DamageEntity)
  git diff HEAD -- ESB/TopicHandlers/StructureHandler.cs (all 17 Structure scope handlers)

### Mechanical translation from old API to new

| Old (IMessenger) | New (IMessageBus) |
|---|---|
| `async Task HandlerName(MessageContext ctx)` | `Task<string> HandlerName(MessageEnvelope env)` |
| `JObject.Parse(ctx.Payload)` | `env.PayloadJson` (already a JObject -- no Parse call needed) |
| `env.PayloadAs<TReq>()` | typed POCO deserialization (preferred when POCO exists in AppPayloads) |
| `await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json)` | `return json` (return the string directly) |
| `await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, err)` | `return err` |
| `args["Key"].Value<T>()` | `(T)args["Key"]` (CLAUDE.md rule -- no .Value<T>()) |
| `_ctx.Messenger.SendAsync("App", MessageType.Evt, "Op", payload)` | `await _ctx.Bus.PublishEventAsync(rcId, "App", "Op", payload)` (rcId chosen per TopicSchema.md sec 11) |
| `GetStructureForEntity(ctx, entityId)` returning null + reply | `GetStructureForEntity(entityId)` returning null silently or throwing |
| `if (ctx.ParsedTopic.MetaOperation != null) ...` | remove (introspection removed) |
| `Describe` / `AppDescribe` handlers | remove (introspection removed) |

Key changes beyond mechanical substitution:
- Old handlers were `async Task` because they awaited ReplyAsync. New handlers are
  `Task<string>` and simply return the JSON string. Use `Task.FromResult(json)` for
  synchronous paths; `async Task<string>` only when awaiting real async work (MainThreadRunner).
- `GetStructureForEntity` in StructureHandler was already changed from
  `async Task<IStructure>(MessageContext)` to `IStructure(int)` -- it now throws
  InvalidOperationException on bad entity type instead of sending an error reply.
  Callers use a try/catch that returns `MessageHelpers.ExceptionJson(ex)`.
- ShowDialogBox fires a dialog-callback event. Old code called `_ctx.Messenger.SendAsync`.
  New code must call `await _ctx.Bus.PublishEventAsync("App", "DialogResponse", payload)`
  inside the void callback using `_ = _ctx.Bus.PublishEventAsync(...)`.

### Scope order and payload files

- App scope: AppHandler.cs / AppPayloads.cs (request and response POCOs complete)
- Player scope: PlayerHandler.cs / PlayerPayloads.cs (add response POCOs as needed)
- Structure scope: StructureHandler.cs / create StructurePayloads.cs as needed

Each implementation returns a JSON string built via JObject or
JsonConvert.SerializeObject(..., MessageHelpers.PascalCaseSettings).

---

## Step 6: Wire DI for handlers that have dependencies -- DEFERRED

No current handler has dependencies beyond ContextData. Revisit when a handler
requires an injected service. BusBuilder.WithServiceProvider is the entry point.

---

## Step 7: Migrate EDNA client subscriptions

EDNA currently subscribes to events using SubscribeBrokerAsync with raw callbacks.
Replace with IMessageBus.OnEvent<T> registrations. Typed payload POCOs from Step 3
replace inline JSON field access throughout EDNA skills and services.

---

## Step 8: Replace remaining direct IMessenger call sites

Any remaining calls to IMessenger.SendAsync, RequestAsync, or PublishRetainedAsync
in application-level code are replaced with the corresponding IMessageBus methods.
After this step, application code no longer holds a reference to IMessenger directly.

Status: BusManager.PublishRegistryEntryAsync was migrated to `Bus.AnnounceAsync` and the Registry
scope is retired. The Broadcast scope (`00000000`) was later removed entirely; Connect is now
published to the participant's current ContextRcId in `GameManager.Init`. Remaining direct
IMessenger calls in application code are the log-message SendAsync invocations in GameManager.Init
and UpdateHandler; these are candidates for `Bus.LogAsync(rcId, ...)` substitution.

---

## Step 9: Expand test coverage

As each handler scope is implemented, add typed integration tests under ESBTests/Bus/
following the pattern in Test_Bus_Integration.cs. Tests verify the full round-trip:
payload serialization, dispatch, handler logic, and response deserialization.

Current integration tests exercise IMessenger-based paths and should continue to pass
throughout the migration.

---

## Step 10: RoutingContextId migration -- DONE

`ConnectionId` was renamed to `RoutingContextId` and is now a per-publish audience
selector rather than a per-participant identity. `IMessageBus` publish/request methods take an
explicit `routingContextId` first parameter; `SubscribeAsync(rcId)` / `UnsubscribeAsync(rcId)` manage
audience subscriptions. Machine rcId and Broadcast rcId are auto-subscribed at connect. See
`Docs/TopicSchema.md` section 11 for the current `RoutingContextKind` taxonomy and the per-kind widths
(Broadcast 8 fixed, Machine 5, Game 8).

Call-site signature deltas previously listed in Step 4 / Step 8 (e.g.
`_ctx.Bus.PublishEventAsync("App", "Op", payload)`) are superseded by the rcId-first form
`_ctx.Bus.PublishEventAsync(rcId, "App", "Op", payload)`.

---

## Step 11: Lobby/Game context unification -- DONE

The pre-game/in-game split was collapsed into a single **ContextRcId** model:

- `RoutingContextKind` is now `{ Broadcast, Machine, Lobby, Game }`. The dead `Playfield`,
  `Player`, and `PlayerInGame` kinds (and their factories) have been removed; any code that
  referenced them is now a hard compile error.
- `RoutingContextId.Lobby(machineId)` derives an 8-char rcId from `"__lobby__" + machineId`.
  Client and EDNA share a MachineId, so they derive the same Lobby rcId and see each other's
  pre-game events.
- A `ContextRcId` field on `GameManager` / `EdnaContext` named the publisher's current audience
  (Pfs/Ds set it to the real Game rcId at startup; Client/EDNA set it to Lobby at connect, swap
  to Game on `GameEnter`, swap back to Lobby on `GameExit`). Step 12 below moved this onto the
  bus itself; ContextRcId now lives on `IMessageBus`.
- Event handlers publish to `ContextRcId` directly with no `?? Broadcast` or `?? MachineId`
  fallback.
- Subscription model is three always-on subs (machine, broadcast, context-evt); the context
  sub is retargeted on enter/exit rather than added/removed.

Addressing rule unchanged: `req`/`res`/`log` always target a recipient MachineId; events publish
to the current ContextRcId; lifecycle (Connect, GameEnter, GameExit) publish to Broadcast.

---

## Step 12: Bus.SwitchContextAsync and PublishContextEventAsync -- DONE

`IMessageBus` gained three members:

- `string ContextRcId { get; }` -- the participant's current audience for events.
- `Task SwitchContextAsync(string newContextRcId)` -- atomically subscribes the new rcId, sets
  `ContextRcId`, then unsubscribes the old one. The new sub is live before the old one drops,
  so no in-process delivery gap exists across the swap. After the swap completes the call
  pauses for `MessageBus.SettleDelayMs` (500 ms) before returning, giving downstream subscribers
  a window to complete their own swap before the caller resumes publishing.
- `Task PublishContextEventAsync<T>(string scope, string operation, T payload)` -- publishes to
  `Bus.ContextRcId`. Callers don't track the rcId; reads happen at send time.

`GameManager.Init` / `EnterGame` / `ExitGame` and `EdnaService.StartAsync` / `OnGameEnter` /
`OnGameExit` were reduced to single `Bus.SwitchContextAsync` calls. All ESB event handlers
now publish via `Bus.PublishContextEventAsync(scope, op, payload)`.

The Client publisher-side race during game-startup is gated by `_ctx.IsTransitioning` (set true
when `GameEventType.GameStarted` arrives, cleared at the end of `EnterGame` / `ExitGame`).
Event handlers queue work into `_ctx.EventQueue` while `IsTransitioning` is true; `UpdateHandler`
drains the queue when `IsReady && !IsTransitioning`. Queued events read `Bus.ContextRcId` at
drain time, so they publish on the new (Game) rcId after the swap completes.

---

## Open: cross-process game-enter race

`SwitchContextAsync` handles the **in-process** swap correctly (no delivery gap on a single
participant). The remaining race is **cross-process**: between the moment Client finishes its
own `SwitchContextAsync(gameRcId)` and the moment EDNA receives the Broadcast `App/evt/GameEnter`
and finishes its own swap, Client may publish events on the new gameRcId. EDNA is still
subscribed to the Lobby rcId during that window and misses those publishes on its broker
connection. Self-loop within Client is fine; the issue is purely the gap on the EDNA side.

Possible follow-on approaches (not yet picked):

1. **Order rearrangement on Client.** Publish `App/evt/GameEnter` to Broadcast **first**, then
   call `SwitchContextAsync(gameRcId)` after a short delay. Subscribers get a head start on
   their own swap. Narrows the window but doesn't eliminate it; depends on broker latency.

2. **Retained context manifest.** A retained message on Broadcast (e.g.
   `Announcements/evt/CurrentContext`) names the current gameRcId. Late joiners can
   `SwitchContextAsync` to it on receipt without waiting for a live `GameEnter`. Combined with
   (1), late-joining participants converge correctly.

3. **Explicit subscriber acknowledgement.** Client publishes `GameEnter`, awaits an `Ack` from
   each known subscriber (or a quorum derived from earlier `Connect` announcements) before
   proceeding to publish on the new rcId. Strongest guarantee, most coordination cost.

4. **Bus-level cross-process primitive.** `IMessageBus.AdvertiseContextSwitchAsync(newRcId)`
   that internally bundles publish-broadcast + wait-quorum + local swap. Centralizes the
   pattern at the cost of further API surface.

Pick when the residual cross-process miss becomes observable (e.g. EDNA missing entity-load
events on game entry). Until then, the in-process correctness from `SwitchContextAsync` plus a
500 ms settle delay inside the swap (`MessageBus.SettleDelayMs`) is the working band-aid; it
buys downstream subscribers time to swap but is timing-dependent, not a real guarantee.

---

## Deferred: middleware and per-message DI scope

Once the above steps are complete:
- Middleware pipeline: logging, validation, retry as a processing stage between bus and handler.
- Per-message DI scope: IServiceScope per handler invocation for scoped services.
