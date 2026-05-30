# Main-Thread Dispatch Audit

## Rule

ModApi/game-state calls run on the main thread (Unity thread affinity).
Topic handlers are invoked on the bus/network thread, so they MUST hop via
`_ctx.MainThreadRunner.RunOnMainThread(...)` around the ModApi lookup + call.
Event handlers are invoked from the game's Update pump (main thread) and read
the ModApi synchronously before any `await`, so they are already on the main
thread and MUST NOT add a hop -- they are audit-only here.

## Pattern (topic handlers)

- Parse the payload and do pure validation OFF the hop (PayloadAs, casts,
  null checks).
- Hop for the entity/structure lookup AND the ModApi call. The lookup
  (`pf.Entities.TryGetValue`, `_ctx.ModApi.ClientPlayfield`) touches the
  ModApi, so it belongs inside the hop.
- Keep the hopped work thin. For large enumerations (e.g. Entity/List) read
  scalar rows inside the hop, build the JSON/Tabular and ToString outside it.
- Method signature becomes `async Task<string>`; `await` the hop. Exceptions
  thrown inside the hop surface to the outer try/catch -> ExceptionJson.
- Model handler: PlayerHandler.Properties / PlayerHandler.Teleport.

## Stage 1 -- Topic handlers (fix)

- [x] EntityHandler -- 8 ops, now hopped. GetProperties + all write/method ops
      hop the lookup + ModApi call; List reads scalar rows in the hop and builds
      the Tabular/ToString off it. (pending user build)
- [x] StructureHandler -- 36 ops, no hop. FIXED via a choke-point wrapper:
      `OnMain(handler)` runs the whole synchronous handler on the main thread
      and Unwraps the completed Task. All 36 method bodies left untouched (zero
      drop risk); only Register was rewritten + the OnMain helper added.
      Follow-up: GetAllBlocks and ScanFloor serialize large block loops on the
      main thread; the block reads must stay on-thread, but if frame cost shows
      up, snapshot blocks then build JSON off-thread. (pending user build)
- [x] AppHandler -- audit found it only hopped the 2 write ops; the 13
      read/async ops called the ModApi off-thread (and ShowDialogBox read
      LocalPlayer.Id off-hop). FIXED: same OnMain wrapper on all 15
      registrations; removed the now-redundant inner hops in SendChatMessage
      and ShowDialogBox. GetStructureAsync/GetStructuresAsync stay async and
      now make their ModApi call on the main thread. (pending user build)
- [x] PlayfieldHandler -- audit found only the async LockStructureDevice
      hopped; the other 21 ops read CurrentPlayfield/pf.* off-thread. FIXED:
      OnMain wrapper on all 22 registrations; removed the inner hop in
      LockStructureDevice (stays async, callback resolves on the main thread).
      (pending user build)
- [x] PlayerHandler -- audit found the player/LocalPlayer resolution ran
      off-hop before the property hop. FIXED: moved the resolution (and its
      error paths) inside the hop in both Properties and Teleport. (pending
      user build)

Stage 1 complete (pending build). All topic handlers now make their ModApi
lookups + calls on the main thread, unified on ONE pattern: the OnMain
choke-point. OnMain lives on a shared base class TopicHandlerBase
(ESB/TopicHandlers/TopicHandlerBase.cs) that holds _ctx + the helper; all five
handlers inherit it, register every op as OnMain(handler), and keep their
bodies synchronous. EntityHandler and PlayerHandler were converted from the
earlier explicit per-op hops to this pattern. OnMain runs parse/serialize on
the main thread too, which is negligible except for the GetAllBlocks/ScanFloor
follow-up noted above (snapshot then serialize off-thread only if frame cost
shows up).

## Stage 2 -- Event handlers (audit only)

Confirm each reads the ModApi only on the main-thread path (synchronously
before the first `await`, or inside the Update-pumped drain). No hop to be
added. Flag any ModApi access that happens after an `await` / on a pool thread.

- [x] GameEventHandler (+ .defs.cs) -- reads GameTicks/Mode and the
      TryAddContainerContents game-state (entity.Structure.GetDevice) before the
      PublishContextEventAsync await. The .defs ItemNameOrId transform reads the
      cached GameManager.BlockAndItemMapping (not a live ModApi call). On main
      thread. CLEAN.
- [x] EntityLoadedHandler -- GameTicks + entity.* read in the top try, before
      the await. CLEAN.
- [x] EntityUnloadedHandler -- same. CLEAN.
- [x] ChatMessageSentHandler -- GameTicks at top; chatMsgData is the event arg
      struct. CLEAN.
- [x] PlayfieldLoadedHandler -- wiring (CurrentPlayfield, += handlers) and all
      playfield.* reads + entity enumeration in the top try, before the await.
      CLEAN.
- [x] PlayfieldUnloadingHandler -- unwire + GameTicks/Name before the await.
      CLEAN.
- [x] GameEnteredHandler -- BuildPayload (reads GameTicks) is called before the
      first await in both branches. Post-await calls are GameManager
      orchestration (AnnounceConnectAsync/EnterGame/ExitGame), not direct ModApi
      reads. CLEAN. Boundary note: if those GameManager methods read the ModApi
      internally they would run post-await; that is a GameManager concern,
      outside this event-handler pass.
- [x] UpdateHandler -- the pump; runs on the Update tick (main thread), only
      drains queues, no ModApi reads of its own. CLEAN.

Stage 2 result: no code changes. Every event handler reads the ModApi/game
state synchronously before its first await, on the main-thread path (the game
fires these on the main thread; deferred ones drain via UpdateHandler, also on
the main thread, still before the first await). After the awaits they touch
only the bus and GameManager. EventHandlerFactory just constructs handlers.

## Stage 3 -- GameManager boundary (follow-up from Stage 2 note)

GameManager is not a handler, but event/handler code calls into it. Audited its
ModApi access (SetGameProperties is the ModApi-heavy method: GetPathFor, Mode,
GetBlockAndItemMapping, Log + ClientPlayfield/Network/... reads).

- GameEnter flow (the boundary Stage 2 flagged): CLEAN in practice.
  GameEnteredHandler calls PrepareEnterGame() BEFORE its first await (main
  thread) -> SetGameProperties runs on the main thread and sets GameRcId. The
  later EnterGame() runs post-await (pool thread) but its
  `if (string.IsNullOrEmpty(GameRcId)) SetGameProperties()` guard is then
  skipped. Latent fragility: that fallback would run ModApi off-thread if
  EnterGame were ever called without PrepareEnterGame first. Currently
  unreachable (sole caller is the handler).
- GameExit flow: ExitGame() touches no ModApi (bus + DisconnectCleanup only).
  AnnounceConnectAsync touches no ModApi. CLEAN.
- REAL finding -- startup Init(): EmpyrionServiceBus.Init (async void) awaits
  _busManager.Init() (MQTT/DB I/O, suspends) and then calls _gameManager.Init().
  With no SynchronizationContext the continuation resumes on a pool thread, so
  GameManager.Init() runs OFF the main thread:
    * all paths: `_ctx.ModApi.Application.Mode` (line 46) read off-thread.
    * Pfs/Ds path: SetGameProperties() (line 53) -> GetPathFor, Mode,
      GetBlockAndItemMapping, ModApi.Log, ClientPlayfield/Network/... all
      off-thread.
  These are Application-level metadata queries (not entity/transform ops), so
  lower-risk than the topic-handler case, but they violate the rule.

Why a plain OnMain hop does NOT work here: Init runs during mod startup, before
the Update pump (UpdateHandler) is ticking, so enqueueing onto MainThreadRunner
and awaiting it would never drain -> deadlock. The clean fix is to read the
ModApi values synchronously on the main thread BEFORE the first await (the game
calls Init on the main thread; ModApi is assigned at InitInternalAsync top,
before `await _busManager.Init()`), then hand the captured values to the async
bus phase. That is a startup-flow restructure of Init/SetGameProperties.

- [x] Decision: ACCEPT AS-IS. The Init() startup ModApi reads (Application.Mode
      on all paths; SetGameProperties on the Pfs/Ds path) are treated as an
      accepted exception to the main-thread rule -- they are one-time startup
      Application-level metadata queries, and Init runs before the Update pump
      so a MainThreadRunner hop would deadlock. No code change. The redundant
      SetGameProperties fallback in EnterGame is also left in place.

## Notes

- Build/test are user-triggered. Do not run tests until implementation is
  declared done.
- HandlerHelper / MessageHelpers are pure serializers called from inside hops;
  no thread concern of their own.
