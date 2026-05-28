# Open Issues

Supersedes Docs/Plans/. Plans directory can be removed once this document is reviewed.

---

## Validate -- complete before proceeding

- [ ] **RunOnMainThread necessity.** Do not assume any RunOnMainThread call is required without testing. For each callsite, remove the wrapper and verify against a running game; restore only on observed null ref or crash. This applies beyond the five callsites currently identified -- any future RunOnMainThread addition must be justified by a failing test without it.

---

## Introspection / Describe

- [ ] **DeviceHandler.cs.** Device scope (LCD, Light, Container, Teleporter) was removed from StructureHandler as dead code during the introspection sprint. Build `DeviceHandler.cs` to restore it under the `Device/` scope with MetaOperation guards and an OpDef table.

---

## Data Serialization

- [ ] **Parse formatted event strings.** Some game events deliver human-readable formatted strings rather than structured fields (e.g. `"'imlarry',1042"` or `"A: 32, R:17"`). Parse these into typed fields before publishing so consumers receive structured data. Audit event handlers for this pattern and define a parsing approach for each affected event.

---

## Handler Alignment

- [ ] **Remove RunOnMainThread wrappers.** Five callsites confirmed: two in `ApplicationHandler.cs` (SendChatMessage, ShowDialogBox), three in `PlayerHandler.cs` (Properties, Teleport, DamageEntity). Remove and test per the validation item above.

- [ ] **Replace JsonConvert for known shapes.** `ApplicationHandler.cs` uses `JsonConvert.SerializeObject(..., PascalCaseSettings)` in several handlers whose output shape is fixed (GetAllPlayfields, GetPlayerEntityIds, GetPfServerInfos, GetBlockAndItemMapping, GetPlayerDataFor). Replace with explicit `JObject` construction. Keep JsonConvert only where the shape is opaque at write time.

- [ ] **Rename bare "ok" response keys.** Four handlers return `"ok": true/false` instead of a named key. Rename: SendChatMessage -> `"Sent"`, ShowDialogBox -> `"Displayed"`, Teleport -> `"Teleport"`, DamageEntity -> `"DamageEntity"`.

---

## Security

- [ ] **Player authentication and TLS.** Implement TLS for the MQTT broker using a single server certificate. Each player authenticates with a unique token that the ESB mod generates and stores on the player's machine at first join. Do not distribute a shared client certificate and do not issue individual client certificates. Token-based auth allows per-player revocation, avoids exposing private keys, and works reliably in a public game environment. MosquittoInstaller will need to configure the broker for TLS and token validation.

---

## EDNAClient Deferred

- [x] **Detection layer (foothold for installers, tray, and lobby navbar).** Landed as `EDNAClient/Setup/ProvisioningDetector.cs`. Done: (a) Steam library lookup via `SteamLocator.GetEmpyrionPath()` -- now also available to the lobby/offline navbar item; (b) ESB mod folder + `ESB_Info.yaml` presence detection; (c) MQTT broker status derived from the actual `Bus.ConnectAsync()` outcome -- classifies `MqttConnectingFailedException.ResultCode` to distinguish `Authenticated` / `AuthFailed` / `Unreachable`; no separate probe runs. Report is published as a `Provisioning/Detection` MQTT log when the bus comes up, and always written to the local EDNA log. Deliberately dropped: local-Mosquitto install detection -- too unreliable across install methods (MSI, chocolatey, scoop, custom path); the connect outcome is ground truth. Remaining: drive non-Ready `StartupState` values (NoEmpyrion, NoEsb) into distinct tray icon variants -- folded into the Tray context menu bullet below.

- [ ] **EDNA UI refinement.** Refine the EDNA user interface. Include an in-app editor for `ESB_Info.yaml` (enabled skills, MQTT connection settings) and a reset-to-defaults action.

- [ ] **EntityOfInterest common model.** Define a shared `EntityOfInterest` model on the client that consolidates tracking-dependent skill data (position, type, owner, threat level, etc.) into a single structure. Back it with one polling output event that reports a list of entities of interest, replacing fragmented per-skill reports.

- [ ] **EsbInstaller.** Stubs exist in `EDNAClient/Setup/`. Implement: copy ESB mod payload into `Empyrion\Content\Mods\ESB\`, write default `ESB_Info.yaml` if absent, detect installed version vs bundled payload for upgrade.

- [ ] **MosquittoInstaller.** Stubs exist in `EDNAClient/Setup/`. Implement: detect existing Mosquitto; if absent, download and run installer; write `mosquitto.conf`; create credentials matching `ESB_Info.yaml`; register and start as a Windows service. LAN-only, no TLS for initial scope; TLS added when Security item above is implemented.

- [ ] **Tray context menu.** Rebuild tray menu driven by `StartupState` (NoEmpyrion / NoEsb / NoMqtt / Ready). Wire EsbInstaller and MosquittoInstaller to menu items based on current state. Tray icon variants for each state (gray / amber / blinking / solid).

- [ ] **Configuration model for per-game-host deployments.** Today `ESB_Info.yaml` is global -- it lives next to the ESB mod, gets baked at Steam-launch time, and carries one MQTT credential set. This doesn't fit multiplayer / dedicated-host scenarios where a game host runs their own broker and wants to decide which EDNA skills clients may activate against their game. Two main approaches: (a) **direct-connect per-host profile** -- EDNA stores broker host/port/creds for each game host the user has joined; on join EDNA disconnects from the prior broker and connects to the host's. Smaller surface; no local-broker mandate. (b) **always-local + Mosquitto bridge** -- every client runs a local Mosquitto; on join, write a bridge entry to mosquitto.conf pointing at the host's broker; EDNA always talks to localhost. Simpler EDNA, but mandates a local broker install on every client and requires bridge-config rewriting + service restart on each join. Either approach also needs a host-published skill allowlist so clients know what they may safely expose. This decision supersedes the existing per-game-yaml tech-debt bullet below and likely reframes the Security (TLS/token) item. Resolve before re-touching MosquittoInstaller or EDNA UI refinement.

- [ ] **Host-published skill allowlist.** Game hosts need a way to declare which EDNA skills clients may activate against their game. Independent of broker topology (applies whether clients direct-connect or bridge via local Mosquitto). Mechanism TBD -- candidates include a retained announcement on a well-known topic at host startup, or a yaml fragment delivered on Client connect. EDNA enforces by gating skill `StartAsync` against the allowlist.

- [ ] **Per-game `ESB_Info.yaml` (broker creds + skill defs).** Today a single `ESB_Info.yaml` is shared across all save games and lives at `{EmpyrionRoot}/Content/Mods/ESB/ESB_Info.yaml`. Each EDNA-aware save also has a `{save}/Content/Mods/ESB/EDNA/` folder where game-scoped state already lives. Future direction: allow a per-save `ESB_Info.yaml` (or a per-save override) to carry game-specific MQTT credentials and game-specific enabled skill set. Tech debt surfaced while designing the lobby/offline navbar -- the level-0 tree now exposes the gap visually, which makes the single-yaml constraint feel out of place.

- [ ] **EDNAClient testing suite.** EDNAClient currently has no automated tests -- regressions are caught by hand against a running game. Stand up a test project for at least the non-WPF, non-MQTT pieces (ProvisioningDetector classification, SteamLocator path handling, NavBuilder tree shape, WorkspaceState round-tripping). Deferred; flagged here so it surfaces in prioritization.

- [ ] **Lobby/offline navbar level-0 (Saves root).** Extend the left navbar tree with a level above the current game-scoped view that maps `{EmpyrionRoot}/Saves/Games/`. Each save folder is a node; show only EDNA-aware saves (those with `Content/Mods/ESB/EDNA/` present). Discover `EmpyrionRoot` via Steam library lookup (libraryfolders.vdf / registry) since in-game `IApplication.GetPathFor(Root)` is unavailable in lobby/offline. In game, the active game node remains the effective root; on leaving the game the full tree reappears with the previously-active game and its expanded nodes still expanded. Default state when entering lobby/offline cold: all nodes collapsed.

---

## Shape Baker

- [ ] **CylinderCubeHalfConnector voxelizes to a full cube.** Surfaced by `Tools/ShapeBaker/find-dupes.ps1` against the current `shapes.bake`: this shape and `Cube` both end up at 512/512 fill (100%), placing them in the same duplicate group. Source mesh has 835 vertices / 987 triangles vs. Cube's 215v/248t, and its AABB is a clean unit cube -- suggests submesh merge is concatenating LOD chains or inner-fill submeshes for this asset, so every voxel ends up inside at least one triangle during `Voxelizer.Voxelize`. Investigate `BundleReader.EnumerateMeshes` (LOD groups, multi-renderer prefabs) and `Program.MergeSubmeshes`; consider filtering submeshes by GameObject owner name (LODn suffixes) or by `MeshRenderer.enabled`. Verification: rerun `find-dupes.ps1` and confirm CylinderCubeHalfConnector drops out of the 100%-fill duplicate group.

- [ ] ** Dups in door baked shape set.**

First call shows every *Prefab root in the models bundle (deduped). The second tells us if door geometry might live in a separate bundle file (we already know about models and models2; there may be more).

What we're testing: the Window_*Prefab family appeared in the models bundle and baked fine, but the same path scheme @models/Blocks/Mothership/DoorNewMSPrefab from BlocksConfig produced no match. Either doors use a non-*Prefab-suffixed root name, they live in a different physical bundle, or BlocksConfig's @models/ prefix is a logical pointer that resolves to a sibling bundle at runtime.

dotnet run --project Tools\ShapeBaker -- --list-bundle-roots models Prefab
dir "C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Bundles"

To collect analysis data.

---

## Audio

- [ ] **Lua audio API.** Expose `EDNAClient/Services/AudioService` to scripts via a new `LuaAudioApi` (mirror `LuaMqttApi` / `LuaLogApi` under `Skills/Scripting/Api/`). Surface at minimum `audio.play_alert(name)`, `audio.play_sfx(path)`, and `audio.speak(text, priority)`. Register through `LuaScriptHost` alongside the existing APIs. Decide how host-side path resolution should work for `play_sfx` (sandbox under `Resources/Audio/` only, or allow absolute paths from trusted scripts).

- [ ] **Audio asset provisioning.** `AudioService` reads from `EDNAClient/Resources/Audio/` (deployed to `<ESB>/EDNA/Resources/Audio/`). No assets are committed yet; `PlayAlert` falls back to `SystemSounds.Asterisk` when a named file is missing. Source or commission a starter set: `ping` (scanner/radar), `alert` (warning), `error`, and a generic confirmation. Decide on licensing (CC0 / public-domain pack vs. commissioned), final format (16-bit WAV vs. MP3), and naming convention so skills can reference alerts by short name with confidence they exist.

---

## Architecture Decisions

- [ ] **V1 GameApi exposure.** Decide which, if any, ModBase (V1) API methods to expose via ESB. ModBase and ModApi (V2) are both active; some V1 calls have no V2 equivalent. Document the decision and implement any chosen handlers.
