# TopicHandler Coverage Analysis

_Generated 2026-03-30 by reviewing ESB/TopicHandlers against Docs/ApiTableOfContents.md._

---

## V1 (ModGameAPI / Mif.dll)

### Implemented

| Handler | Topics |
|---------|--------|
| Blueprint | Finish, Resources |
| Faction | List, AlliancesAll, AlliancesByFaction |
| Message | ToPlayer, ToAll, ToFaction, Dialog |
| Player | GetInventory, GetInfo, List, GetAndRemoveInventory, SetInventory, AddItem, ItemExchange, GetCredits, SetCredits, AddCredits, SetInfo, ChangePlayfield |
| Playfield | List, Stats, Load, EntityList |
| Server | Stats, ConsoleCommand, BannedPlayers |
| Structure | ListGlobal, Update, Touch, BlockStats |

### Partial: V1 Playfield events

`Event_Playfield_Loaded` fires and is published, but the payload is empty -- `PlayfieldLoad` fields (`playfield`, `isPvP`, `processId`) are not serialized into the MQTT message.

---

### Gap: V1.Entity handler (not implemented)

The following `CmdId` entries form a coherent entity-management group with no handler:

| CmdId | Value | Notes |
|-------|-------|-------|
| Request_Entity_PosAndRot | 39 | Get position and rotation of any entity by ID |
| Request_Entity_Teleport | 36 | Teleport any entity (not just players) |
| Request_Entity_ChangePlayfield | 37 | Move any entity between playfields |
| Request_Entity_Spawn | 42 | Spawn an entity |
| Request_Entity_Destroy | 38 | Destroy an entity |
| Request_Entity_Destroy2 | 70 | Alternate destroy variant |
| Request_Entity_Export | 71 | Export entity as blueprint |
| Request_Entity_SetName | 73 | Rename an entity |
| Request_NewEntityId | 46 | Reserve a new entity ID before spawning |

**Decision:** candidate for a new `V1.Entity` handler. PosAndRot, Teleport, Spawn, Destroy, and SetName are the most immediately useful.

### Gap: V1 event subscriptions (not implemented)

These fire from the game unprompted. Not request/response -- require a publish pattern:

| CmdId | Value | Notes |
|-------|-------|-------|
| Event_ChatMessage / ChatMessageEx | 53 / 54 | Incoming chat messages |
| Event_Faction_Changed | 41 | Player faction membership changed |
| Event_TraderNPCItemSold | 65 | Item sold at a trader NPC |
| Event_PdaStateChange | 74 | PDA task state transition |

**Decision:** deferred -- requires designing a publish/event pattern distinct from request/response.

---

## V2 (IModApi / ModApi.dll)

### Implemented

| Handler | Topics | Notes |
|---------|--------|-------|
| Application | GetPathFor, GetAllPlayfields, GetPfServerInfos, GetPlayerEntityIds, GetPlayerDataFor, SendChatMessage, ShowDialogBox, GetStructure, GetStructures, GetBlockAndItemMapping, State, Mode, LocalPlayer, GameTicks | All IApplication methods and properties covered |
| Block | Get, Set, SetDamage, GetTextures, SetTextures, SetTextureForWholeBlock, GetColors, SetColors, SetColorForWholeBlock, GetSwitchState, SetSwitchState, SetLockCode | Full IBlock surface covered |
| Container | Get, Contains, GetTotalItems, AddItems, RemoveItems, Clear, SetContent | Full IContainer surface covered |
| Gui | ShowGameMessage, ShowDialog, IsWorldVisible | Full IGui surface covered |
| Lcd | Get, SetText, SetTextColor, SetBackgroundColor, SetFontSize | Full ILcd surface covered |
| Light | Get, SetColor, SetIntensity, SetRange, SetLightType, SetBlinkData, SetSpotAngle | Full ILight surface covered |
| Player | Properties (V2.Player), Teleport, DamageEntity | Partial -- see gaps |
| Playfield | SpawnEntity, SpawnPrefab, RemoveEntity, IsStructureDeviceLocked, Info, MoveEntity | Partial -- see gaps |
| Structure | Info, Tanks, GetAllCustomDeviceNames, GetDevicePositions, SetFaction, GetDockedVessels, GetPassengers, GetBlockSignals, GetControlPanelSignals, GetSignalState, GetSignalReceivers, GetSendSignalName, StructToGlobalPos, GlobalToStructPos, AddTankContent | Partial -- see gaps |
| Teleporter | Get, Set | Full ITeleporter surface covered |
| Utilities | TestSelf, Teleport, DumpMemory, WindowInfo, TraceEntity, ShowEntity | Diagnostic helpers |

### Dead code: V2.Pda

All 15 topics in `ESB/TopicHandlers/V2/Pda.cs` are non-functional. ESB runs as a server-side mod; `IModApi.PDA` is null in that context. The handler is retained as documentation of the IPda surface but will never execute.

**Decision:** leave in place, marked dead. Do not extend or maintain.

### Gap: IModApi interfaces -- not implemented

| Interface | Decision |
|-----------|----------|
| INetwork | **Will not implement.** No use case in the MQTT bridge design. |
| IScript | **Will not implement.** No use case in the MQTT bridge design. |
| ISoundPlayer | **Will not implement.** No use case in the MQTT bridge design. |

### Gap: IContainer members not exposed

| Member | Notes | Decision |
|--------|-------|----------|
| `VolumeCapacity` (set) | Write setter for container volume capacity | Candidate |
| `DecayFactor` (set) | Write setter for container decay rate | Candidate |

### Gap: IPlayer methods not exposed

| Method | Decision |
|--------|----------|
| `MoveForward(float speed)` | Skip -- not useful server-side |
| `Move(Vector3 direction)` | Skip -- not useful server-side |
| `MoveStop()` | Skip -- not useful server-side |
| `LoadFromDSL()` | Candidate -- low priority |

### Gap: IPlayfield members not exposed

| Member | Notes | Decision |
|--------|-------|----------|
| `Players` (Dictionary) | All players currently on this playfield | Candidate |
| `Entities` (Dictionary) | All entities currently on this playfield | Candidate |
| `GetTerrainHeightAt(x, z)` | Terrain height probe | Candidate |
| `LockStructureDevice(...)` | Lock/unlock a specific device | Candidate |
| `AddVoxelArea / MoveVoxelArea / RemoveVoxelArea` | Voxel manipulation | Skip -- niche |
| `SpawnTestPlayer(...)` | Spawn a test player instance | Skip -- test tooling only |
| `RemoveTestPlayer(...)` | Remove a test player instance | Skip -- test tooling only |

### Gap: IStructure members not exposed

| Member | Notes | Decision |
|--------|-------|----------|
| `Pilot` (IPlayer) | The player currently piloting the structure | Candidate -- add to V2.Structure.Info |
| `SetColorOfBlocks(List<BlockPosColor>, BlockSide)` | Bulk block colour setter | Candidate |
| `GetDevices(DeviceTypeName)` | Enumerate all devices of a named type | Candidate |
| `GetDevice<T>(int, int, int)` | Typed device accessor by coordinates | Candidate |
| `GetDevice<T>(string)` | Typed device accessor by name | Candidate |
| `GetDevice<T>(VectorInt3)` | Typed device accessor by VectorInt3 | Candidate |
| `GetDevice<T>(Vector3)` | Typed device accessor by Vector3 | Candidate |
| `GetBlock(VectorInt3)` | Block accessor by VectorInt3 | Candidate |
| `GetBlock(int, int, int)` | Block accessor by coordinates | Candidate |
| `Entity` (IEntity) | The IEntity associated with this structure | Candidate |

### Gap: IApplication events not subscribed

These fire from the game unprompted. Not request/response -- require a publish pattern:

| Event | Notes |
|-------|-------|
| ChatMessageSent | Server-side chat intercept |
| GameEntered | Game fully loaded |
| OnPlayfieldLoaded | Playfield came online |
| OnPlayfieldUnloading | Playfield going offline |
| FixedUpdate | Per-tick callback |

**Decision:** deferred -- same event/publish pattern question as V1 events above.

---

## Open Questions

1. **V1.Entity handler** -- confirm which of the nine CmdIds to implement before starting.
2. **Event publishing** -- design a consistent topic pattern for unprompted game events (V1 and V2) before implementing any of them.
3. **IPlayfield.Players / Entities** -- confirm these are populated and accessible in the ESB server-side context before implementing.
4. **IPlayer.Teleport overload** -- ApiTableOfContents shows only `Teleport(Vector3 pos)` declared on IPlayer; the cross-playfield overload `Teleport(string playfield, Vector3 pos, Vector3 rot)` is used in the handler. Verify it is present in the DLL (may be on IEntity or inherited).
