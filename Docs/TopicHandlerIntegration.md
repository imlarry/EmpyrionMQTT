# Topic Handler Integration

Gap analysis of V1 (EmpyrionModBase requests and events) and V2 (IModApi) coverage across `ESB/TopicHandlers`.
API reference: `Docs/api/` and `EmpyrionNetAPIAccess`.

V1 operations are grouped by domain. Each domain covers both the `Request_*` call that queries the game
and the corresponding `Event_*` delegate that receives the response or push notification.

---

## Partially Completed

APIs where one or more methods or properties are not exposed.

### V1

**Player** (Requests) — `Request_Player_List`, `Request_Player_Info`, `Request_Player_SetInventory`,
`Request_Player_AddItem`, `Request_Player_Credits`, `Request_Player_SetCredits`,
`Request_Player_AddCredits`, `Request_Player_ChangePlayerfield`, `Request_Player_ItemExchange`,
`Request_Player_SetPlayerInfo`, `Request_Player_GetAndRemoveInventory`

> `Request_Player_GetInventory` is the only Player request exposed (via `Application.Player_GetInventory`).

**Playfield** (Events) — `Event_Playfield_Loaded` fires but publishes an empty payload; `PlayfieldLoad`
fields (`playfield`, `isPvP`, `processId`) are not serialized into the MQTT message.

---

### V2

**IContainer** — `VolumeCapacity` [set], `DecayFactor` [set]

**IPda** — `ShowPdaChapterBriefing`, `CreateTimer`, `StartTimer`, `StopTimer`, `KillAllTimers`, `GetPlayfieldByPlanetType`

**IPlayer** — `DamageEntity`, `MoveForward`, `Move`, `MoveStop`, `LoadFromDSL`

**IPlayfield** — `LockStructureDevice`, `AddVoxelArea`, `MoveVoxelArea`, `RemoveVoxelArea`, `SpawnTestPlayer`, `RemoveTestPlayer`, `GetTerrainHeightAt`, `Players`, `Entities`

**IStructure** — `GetDevices`, `GetDevice<T>(int, int, int)`, `GetDevice<T>(string)`, `GetDevice<T>(VectorInt3)`, `GetDevice<T>(Vector3)`, `GetBlock(VectorInt3)`, `GetBlock(int, int, int)`, `Entity`

---

## Not Started

APIs with no topic handler or interface.

### V1

**Entity** — `Request_Entity_Teleport`, `Request_Entity_ChangePlayfield`, `Request_Entity_Destroy`,
`Request_Entity_Destroy2`, `Request_Entity_PosAndRot`, `Request_Entity_Spawn`,
`Request_Entity_Export`, `Request_Entity_SetName`, `Request_NewEntityId`

**Playfield** — `Request_Playfield_List`, `Request_Playfield_Stats`, `Request_Load_Playfield`,
`Request_Playfield_Entity_List`

**Structure** — `Request_GlobalStructure_List`, `Request_GlobalStructure_Update`,
`Request_Structure_Touch`, `Request_Structure_BlockStatistics`

**Faction** — `Request_Get_Factions`, `Request_AlliancesAll`, `Request_AlliancesFaction`

**Admin** — `Request_Dedi_Stats`, `Request_ConsoleCommand`, `Request_GetBannedPlayers`,
`Request_InGameMessage_SinglePlayer`, `Request_InGameMessage_AllPlayers`,
`Request_InGameMessage_Faction`, `Request_ShowDialog_SinglePlayer`

**Blueprint** — `Request_Blueprint_Finish`, `Request_Blueprint_Resources`

**Player Events** — `Event_Player_Connected`, `Event_Player_Disconnected`,
`Event_Player_ChangedPlayfield`, `Event_Player_DisconnectedWaiting`

**Playfield Events** — `Event_Playfield_Unloaded`

**Faction Events** — `Event_Faction_Changed`

**Game Events** — `Event_ChatMessage`, `Event_ChatMessageEx`, `Event_ConsoleCommand`,
`Event_PdaStateChange`, `Event_GameEvent`, `Event_Statistics`, `Event_TraderNPCItemSold`

### V2

- IEntity
- INetwork
- IPortal
- ISoundPlayer

---

## Completed

APIs where all methods and properties are exposed.

### V1

*(none)*

### V2

- IApplication
- IBlock
- IGui
- ILcd
- ILight
- ITeleporter
