# Open Issues

Supersedes Docs/Plans/. Plans directory can be removed once this document is reviewed.

---

## Validate -- complete before proceeding

- [ ] **Coop/Pfs: Structure handlers reliable at startup.** `Structure/req/Info` (and other Structure ops) should return data immediately on a Pfs participant without requiring a playfield transition. The StartupEventCapture event queue addresses the PlayfieldLoadedHandler startup race; confirm in a live coop session.

- [ ] **Coop/Client: LocalPlayer null.** `Player/req/GetProperties`, `Player/req/Teleport`, and `Player/req/DamageEntity` fail with "LocalPlayer is null" on a coop client in some configurations. PlayerHandler still calls `IModApi.Application.LocalPlayer` directly. If validation confirms the gap is real, fix by resolving the local player via entity-loaded event filtering rather than the direct API property.

- [ ] **RunOnMainThread necessity.** Do not assume any RunOnMainThread call is required without testing. For each callsite, remove the wrapper and verify against a running game; restore only on observed null ref or crash. This applies beyond the five callsites currently identified -- any future RunOnMainThread addition must be justified by a failing test without it.

---

## Investigate

- [ ] **BlockAndItemMapping fires before game is loaded.** The Client participant is receiving `Registry/evt/BlockAndItemMapping` before a game session is active. Determine what triggers the early publish; likely needs a game-state guard before the event fires, or the issue resolves once Registry entries are game-scoped (see Registry section below).

---

## Introspection / Describe

- [ ] **StructureHandler OpDef table.** `ApplicationHandler.defs.cs` and `PlayerHandler.defs.cs` each have an `_opDefs` table; `StructureHandler.Defs.cs` does not. Add it so `Structure/Describe` returns the full scope manifest.

- [ ] **DeviceHandler.cs.** Device scope (LCD, Light, Container, Teleporter) was removed from StructureHandler as dead code during the introspection sprint. Build `DeviceHandler.cs` to restore it under the `Device/` scope with MetaOperation guards and an OpDef table.

---

## Registry

- [ ] **Remove old-form Registry topic.** An old-format `Registry/` topic is still being published. Find the callsite and remove it; all registry traffic should use the current ESB/ topic schema.

- [ ] **Game-scope Registry entries.** Many or all Registry entries need to include the game ID so consumers can distinguish entries across game sessions (and discard stale entries from a prior session). Design the scoping approach -- likely a game-id segment or a payload field -- before implementing.

---

## Data Serialization

- [ ] **Array-of-arrays packing for repeated-key payloads.** Where handler responses include arrays of objects with identical keys on every row, replace with array-of-arrays format: a header row followed by value rows. This removes per-row key repetition and aligns with how structured tabular data is efficiently represented. Identify which handlers produce this pattern before implementing.

- [ ] **Parse formatted event strings.** Some game events deliver human-readable formatted strings rather than structured fields (e.g. `"'imlarry',1042"` or `"A: 32, R:17"`). Parse these into typed fields before publishing so consumers receive structured data. Audit event handlers for this pattern and define a parsing approach for each affected event.

---

## Handler Alignment

- [ ] **Remove RunOnMainThread wrappers.** Five callsites confirmed: two in `ApplicationHandler.cs` (SendChatMessage, ShowDialogBox), three in `PlayerHandler.cs` (Properties, Teleport, DamageEntity). Remove and test per the validation item above.

- [ ] **Fix ClientPlayfield reference.** `ApplicationHandler.cs` `ModApiProperties` handler still references `_ctx.ModApi.ClientPlayfield`. Replace with `_ctx.GameManager.CurrentPlayfield != null ? "set" : "null"` to match how all other handlers report playfield state.

- [ ] **Consolidate BuildStructureJson.** A private copy lives in `ApplicationHandler.cs` and a static copy in `StructureHandler.Defs.cs`. Pick one home, remove the duplicate.

- [ ] **Replace JsonConvert for known shapes.** `ApplicationHandler.cs` uses `JsonConvert.SerializeObject(..., PascalCaseSettings)` in several handlers whose output shape is fixed (GetAllPlayfields, GetPlayerEntityIds, GetPfServerInfos, GetBlockAndItemMapping, GetPlayerDataFor). Replace with explicit `JObject` construction. Keep JsonConvert only where the shape is opaque at write time.

- [ ] **Rename bare "ok" response keys.** Four handlers return `"ok": true/false` instead of a named key. Rename: SendChatMessage -> `"Sent"`, ShowDialogBox -> `"Displayed"`, Teleport -> `"Teleport"`, DamageEntity -> `"DamageEntity"`.

---

## Security

- [ ] **Player authentication and TLS.** Implement TLS for the MQTT broker using a single server certificate. Each player authenticates with a unique token that the ESB mod generates and stores on the player's machine at first join. Do not distribute a shared client certificate and do not issue individual client certificates. Token-based auth allows per-player revocation, avoids exposing private keys, and works reliably in a public game environment. MosquittoInstaller will need to configure the broker for TLS and token validation.

---

## ESB.Messaging Follow-on

- [ ] **Sprint 3: Subscription helpers.** Add helper methods for the two most common wildcard subscription patterns: all req for a given scope+operation from any participant (`ESB/+/+/{scope}/req/{operation}`); all evt from a specific participant type (`ESB/{participantType}/+/+/evt/+`).

- [ ] **Sprint 4: Fan-out request pattern.** Document the fan-out pattern (many subscribers on a req topic, one responds via ResponseTopic header) and decide whether a helper wrapper is warranted.

---

## EDNAClient Deferred

- [ ] **EDNA UI refinement.** Refine the EDNA user interface. Include an in-app editor for `ESB_Info.yaml` (enabled skills, MQTT connection settings) and a reset-to-defaults action.

- [ ] **EntityOfInterest common model.** Define a shared `EntityOfInterest` model on the client that consolidates tracking-dependent skill data (position, type, owner, threat level, etc.) into a single structure. Back it with one polling output event that reports a list of entities of interest, replacing fragmented per-skill reports.

- [ ] **EsbInstaller.** Stubs exist in `EDNAClient/Setup/`. Implement: copy ESB mod payload into `Empyrion\Content\Mods\ESB\`, write default `ESB_Info.yaml` if absent, detect installed version vs bundled payload for upgrade.

- [ ] **MosquittoInstaller.** Stubs exist in `EDNAClient/Setup/`. Implement: detect existing Mosquitto; if absent, download and run installer; write `mosquitto.conf`; create credentials matching `ESB_Info.yaml`; register and start as a Windows service. LAN-only, no TLS for initial scope; TLS added when Security item above is implemented.

- [ ] **Tray context menu.** Rebuild tray menu driven by `StartupState` (NoEmpyrion / NoEsb / NoMqtt / Ready). Wire EsbInstaller and MosquittoInstaller to menu items based on current state. Tray icon variants for each state (gray / amber / blinking / solid).

- [ ] **MQTT v5 ESB-to-EDNA routing.** Replace the connection-mapping cache with deterministic response topic routing using MQTT 5 `ResponseTopic` and `CorrelationData` headers for ESB-to-EDNA round trips.

---

## Architecture Decisions

- [ ] **V1 GameApi exposure.** Decide which, if any, ModBase (V1) API methods to expose via ESB. ModBase and ModApi (V2) are both active; some V1 calls have no V2 equivalent. Document the decision and implement any chosen handlers.
