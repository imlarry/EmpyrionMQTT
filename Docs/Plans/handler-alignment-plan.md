# Context

ApplicationHandler and PlayerHandler predate the ESB/ topic schema refactor and the StructureHandler implementation that established the canonical style for new handlers. Both have accumulated inconsistencies that will confuse future contributors and make cross-handler patterns harder to spot. Since no external consumers exist yet, this is the right time to align them.

The goal is to bring ApplicationHandler and PlayerHandler structurally in line with StructureHandler -- same file layout, same serialization approach, same no-RunOnMainThread default, same playfield reference, same response key naming -- without changing any observable behavior or topic structure.

---

# Critical Files

- `ESB/TopicHandlers/StructureHandler.cs` -- canonical style reference
- `ESB/TopicHandlers/StructureHandler.Defs.cs` -- canonical partial class pattern
- `ESB/TopicHandlers/ApplicationHandler.cs` -- to be refactored + split
- `ESB/TopicHandlers/PlayerHandler.cs` -- to be refactored + split
- `ESB/TopicHandlers/HandlerHelper.cs` -- may gain TryGetRequired<T> helper
- `ESB/BusService/GameManager.cs` -- CurrentPlayfield source of truth

---

# Changes

## 1. ApplicationHandler -- split and clean

**Create `ApplicationHandler.Defs.cs`** (partial class) containing:
- `BuildStructureJson(GlobalStructureInfo)` -- moved from the bottom of ApplicationHandler.cs (currently lines 365-379)
- Any other static helpers that accumulate

**In `ApplicationHandler.cs`**:
- Remove `BuildStructureJson` (moved to Defs)
- `LocalPlayer` handler: remove `RunOnMainThread` wrapper -- LocalPlayer property reads are safe off-thread (GameTicks/State/Mode already prove this; the wrapper was applied with a broad brush per prior discussion)
- `SendChatMessage`: remove `RunOnMainThread` -- no evidence this needs main thread; test and add back only if null refs occur
- `ShowDialogBox`: same -- remove `RunOnMainThread`, test
- `ModApiProperties`: replace `_ctx.ModApi.ClientPlayfield` reference (line 60) with `_ctx.GameManager.CurrentPlayfield != null ? "set" : "null"` so it reports the same thing as the others but uses the correct cross-participant reference
- Replace `JsonConvert.SerializeObject(..., PascalCaseSettings)` calls with explicit `JObject` construction where the shape is known (GetAllPlayfields, GetPlayerEntityIds, GetPfServerInfos, GetBlockAndItemMapping, GetPlayerDataFor). Where the shape is opaque (PlayerData, PfServerInfos), keep JsonConvert -- it's appropriate for unknown shapes.
- Response keys: replace `"ok": true` in SendChatMessage and ShowDialogBox with a descriptive key (`"Sent": true`, `"Displayed": true`) to match StructureHandler convention

## 2. PlayerHandler -- split and clean

**Create `PlayerHandler.Defs.cs`** (partial class) containing:
- `PlayerProp` flags enum
- `PropDef` struct
- `Props` static array
- `ValidPropertyNames` static array
- `Vec3()`, `Vec4()`, `FD()` static helpers
- `SerializeItemStacks()` static helper
- `TryParseMask()` static helper

**In `PlayerHandler.cs`**:
- Remove all of the above (moved to Defs)
- `Properties` handler: remove `RunOnMainThread` wrapper -- the PropDef getter loop reads IPlayer properties; same reasoning as LocalPlayer above -- test without it
- `Teleport` handler: remove `RunOnMainThread` wrapper; `lp.Teleport()` is a method call on IPlayer, not a Unity/game-thread-only operation per the broader-brush theory
- `DamageEntity` handler: same -- remove wrapper
- Response key: `Teleport` currently returns `{"ok": result}` -- change to `{"Teleport": result}` to match StructureHandler's named-key convention. `DamageEntity` currently returns `{"ok": true}` -- change to `{"DamageEntity": true}`

## 3. HandlerHelper -- optional, low priority

If the repeated `args["X"] == null` + `ReplyErrorAsync` pattern in call/handlers becomes noticeable after the above cleanup, add:

```csharp
internal static bool TryGetRequired(JObject args, string key, out JToken value, IMessenger messenger, MessageContext ctx)
```

Do not add this speculatively -- only if at least 3 handlers benefit.

---

# Conventions to Enforce (StructureHandler style)

| Rule | Rationale |
|---|---|
| No `RunOnMainThread` by default | Applied with broad brush; remove and add back only on observed null ref |
| `_ctx.GameManager.CurrentPlayfield` not `ClientPlayfield` | Works on Client and Pfs |
| Static helpers in `.Defs.cs` companion file | Keeps handler bodies readable; Defs file is pure data |
| `JObject` construction, not `JsonConvert` for known shapes | Explicit, no hidden settings, consistent |
| Named response keys, never bare `"ok"` | Self-describing payloads; topic already carries the operation name |
| All handler methods `public async Task Name(MessageContext ctx)` | Consistent, testable |

---

# What a verbal instruction alone would miss

A verbal instruction ("align ApplicationHandler and PlayerHandler with StructureHandler") is almost sufficient, but would leave these decisions ambiguous without this plan:

1. **Which RunOnMainThread calls to remove** -- the answer is all of them, then test; without the plan a future session might leave some based on guesswork
2. **ClientPlayfield in ModApiProperties** -- easy to overlook since it's one of seven null-checks that look like diagnostics
3. **JsonConvert for opaque shapes** -- a blanket "replace JsonConvert" would break GetPlayerDataFor and GetPfServerInfos where the shape isn't known at write time
4. **"ok" key replacement** -- which descriptive key to use per handler is a judgment call that benefits from being decided once

---

# Verification

1. Build: `dotnet build` -- zero errors, zero new warnings
2. Run integration tests: `dotnet test --filter "Category=Integration"` -- all tests that passed before still pass
3. Manually test `Req/App/get/LocalPlayer`, `Req/App/call/SendChatMessage`, `Req/Player/call/Teleport`, `Req/Player/call/DamageEntity` against a running game to confirm RunOnMainThread removal doesn't produce null refs
4. Confirm `ModApiProperties` response still reports "set"/"null" correctly on a Client session
