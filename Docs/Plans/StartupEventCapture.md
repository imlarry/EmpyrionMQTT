# Context

The ESB startup sequence runs async: MQTT connects, subscriptions register, then `EnableEventHandlers()` attaches game API delegates. Any game event that fires during that window (MQTT connect + subscribe latency) is silently dropped. The most likely victim is `Application.GameEntered`, which fires as the player loads a save -- precisely when the mod is initializing.

The problem is structural: delegates are registered *after* async I/O completes, but the game can raise events at any time after `Init()` is called.

---

# Startup Timeline (current)

```
Init() called by game
  InitInternalAsync()
    factory.Create*Handler()        -- handlers built, no delegates yet
    BusManager.Init()
      ConnectAsync()                -- MQTT handshake (network I/O, ~100-500ms)
      SubscribeAll()                -- topic subscriptions
      EnableEventHandlers()         -- delegates registered HERE
    GameManager.Init()              -- "Created" status published
```

**Window of exposure:** everything between `Init()` entry and `EnableEventHandlers()` return.

---

# Solution -- Persistent Event Queue with IsReady Drain Gate

Register all game API delegates immediately at the top of `InitInternalAsync()`, before any async work. All events funnel through a persistent `Queue<Func<Task>>` on `ContextData`. `HandlerBase.Execute()` always enqueues rather than running work directly. `UpdateHandler` drains the queue on every game tick, but only when `IsReady` is true. `BusManager.Init()` sets `IsReady = true` after MQTT connect and subscribe complete.

This means:
- No events are dropped during startup -- they accumulate in the queue.
- After `IsReady`, events drain naturally on the next Update tick(s), in arrival order.
- No special flush-once logic; no edge case around events arriving during a flush.
- The queue remains in the flow permanently, handling any future burst situations as a side effect.

**Startup Timeline (proposed):**

```
Init() called by game
  InitInternalAsync()
    factory.Create*Handler()
    EnableEventHandlers()           -- delegates registered IMMEDIATELY
    BusManager.Init()
      ConnectAsync()                -- events queue during this window
      SubscribeAll()
      IsReady = true                -- drain gate opens
    GameManager.Init()
```

---

# Modules Touched

- **[ESB/BusService/ContextData.cs](ESB/BusService/ContextData.cs)** -- add `bool IsReady` and `Queue<Func<Task>> EventQueue`
- **[ESB/EventHandlers/HandlerBase.cs](ESB/EventHandlers/HandlerBase.cs)** -- `Execute()` enqueues the work closure instead of running it directly
- **[ESB/EventHandlers/UpdateHandler.cs](ESB/EventHandlers/UpdateHandler.cs)** -- drain loop: dequeue and invoke all pending work while `IsReady`
- **[ESB/BusService/BusManager.cs](ESB/BusService/BusManager.cs)** -- set `IsReady = true` after `SubscribeAll()` completes; remove `EnableEventHandlers()` call
- **[ESB/BusService/ESB.cs](ESB/BusService/ESB.cs)** -- call `EnableEventHandlers()` immediately after handler construction, before `BusManager.Init()`
- **[ESB/EventHandlers/EventManager.cs](ESB/EventHandlers/EventManager.cs)** -- no structural change; call site moves to `ESB.cs`

---

# Verification

1. Build and deploy to game.
2. Start a save game; confirm `Application.GameEnter` event arrives on MQTT after startup completes.
3. Confirm no exceptions in the ESB log during the startup window.
4. Confirm high-frequency `Update` drain does not introduce observable lag (event queue should be empty within one tick under normal load).
