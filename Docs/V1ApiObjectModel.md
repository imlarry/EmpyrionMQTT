# V1 ModGameAPI — Object Model Reference

The V1 API (`ModGameAPI` / `ModInterface` / `EmpyrionModBase`) is an async RPC layer available **only on DedicatedServer in multiplayer**. Every operation is a matched pair: a `Request_X` that sends data and an `Event_X` that delivers the response.

`EmpyrionModBase` wraps these pairs into `Task<T>` methods (e.g. `Request_Player_GetInventory(id)`), hiding the raw CmdId dispatch.

All V1 operations are unreachable in SinglePlayer and on PlayfieldServer/Client processes. Handlers registered for V1 topics silently receive no messages outside of a dedicated server.

---

## Availability Summary

| Layer | Process | Game Mode | V1 Available |
|-------|---------|-----------|--------------|
| V1 ModBase | DedicatedServer | MP only | Yes |
| V1 ModBase | PlayfieldServer | MP only | No (likely) |
| V1 ModBase | Client | SP or MP | No |

---

## Object Groups

### Player

Operations against any player by entity id — including players on other playfields and offline players known to the server.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Player.GetInfo` | `Request_Player_Info` → `Event_Player_Info` | `Id(entityId)` | `PlayerInfo` | Partial: V2 `IPlayer` on Client is local player only |
| `V1.Player.List` | `Request_Player_List` → `Event_Player_List` | `Id(0)` | `PlayerInfoSet` | Partial: V2 `Application.GetPlayerEntityIds()` — ids only, no full info |
| `V1.Player.GetInventory` | `Request_Player_GetInventory` → `Event_Player_Inventory` | `Id(entityId)` | `Inventory` | Partial: V2 `IPlayer.Toolbar/Bag` on Client is local player only |
| `V1.Player.SetInventory` | `Request_Player_SetInventory` | `Inventory(playerId, toolbelt, bag)` | `Event_Ok` | None |
| `V1.Player.GetAndRemoveInventory` | `Request_Player_GetAndRemoveInventory` → `Event_Player_GetAndRemoveInventory` | `Id(entityId)` | `Inventory` | None |
| `V1.Player.AddItem` | `Request_Player_AddItem` | `IdItemStack(entityId, itemStack)` | `Event_Ok` | None |
| `V1.Player.ItemExchange` | `Request_Player_ItemExchange` → `Event_Player_ItemExchange` | `ItemExchangeInfo` | `ItemExchangeInfo` | None |
| `V1.Player.GetCredits` | `Request_Player_Credits` → `Event_Player_Credits` | `Id(entityId)` | `IdCredits` | Partial: V2 `IPlayer.Credits` is local player only |
| `V1.Player.SetCredits` | `Request_Player_SetCredits` | `IdCredits(entityId, credits)` | `Event_Ok` | None |
| `V1.Player.AddCredits` | `Request_Player_AddCredits` | `IdCredits(entityId, amount)` | `Event_Ok` | None |
| `V1.Player.SetInfo` | `Request_Player_SetPlayerInfo` | `PlayerInfo` | `Event_Ok` | None |
| `V1.Player.ChangePlayfield` | `Request_Player_ChangePlayerfield` | `IdPlayfieldPositionRotation` | `Event_Ok` | None — V2 `IPlayer.Teleport(playfield, pos, rot)` only applies to the local player |

**`PlayerInfo` fields:** entityId, steamId, playerName, playfield, pos, rot, health/food/oxygen/etc., credits, toolbar/bag, exp, upgrade, ping, permission, faction

**`Inventory` fields:** playerId, toolbelt[], bag[]  — item stacks only, no health/stats

**When to use V1 Player vs V2:**
- **V2 preferred**: local player stats, inventory, position — instant, no round-trip, works in SP
- **V1 required**: any other player's data, any write operation (set inventory, credits, teleport across playfields), connect/disconnect tracking

---

### Entity

Operations against any entity (structures, vessels, NPCs) by entity id, server-wide.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Entity.GetPosAndRot` | `Request_Entity_PosAndRot` → `Event_Entity_PosAndRot` | `Id(entityId)` | `IdPositionRotation` | Partial: V2 `IEntity.Position/Rotation` within local playfield only |
| `V1.Entity.Teleport` | `Request_Entity_Teleport` | `IdPositionRotation` | `Event_Ok` | None — V2 has no entity teleport |
| `V1.Entity.ChangePlayfield` | `Request_Entity_ChangePlayfield` | `IdPlayfieldPositionRotation` | `Event_Ok` | None |
| `V1.Entity.Destroy` | `Request_Entity_Destroy` / `Request_Entity_Destroy2` | `Id(entityId)` | `Event_Ok` | None |
| `V1.Entity.Spawn` | `Request_Entity_Spawn` | `EntitySpawnInfo` | `Event_Ok` | Partial: V2 `IPlayfield.SpawnEntity/SpawnPrefab` is local-playfield-scope only |
| `V1.Entity.SetName` | `Request_Entity_SetName` | `IdPlayfieldName(entityId, name)` | `Event_Ok` | None |
| `V1.Entity.Export` | `Request_Entity_Export` | `EntityExportInfo` | `Event_Ok` | None |
| `V1.Entity.NewId` | `Request_NewEntityId` → `Event_NewEntityId` | _(none)_ | `Id(newEntityId)` | None |

**`EntitySpawnInfo` fields:** forceEntityId, playfield, pos, rot, name, type, entityTypeName, prefabName, prefabDir, factionGroup, factionId, exportedEntityDat

**When to use V1 Entity:**
- Cross-playfield entity operations (teleport, move, destroy) require V1 — V2 playfield methods only reach entities on the locally loaded playfield
- Server-side entity spawning with full faction/type control

---

### Structure

Server-wide structure queries including structures on idle (unloaded) playfields — the key capability unavailable in V2.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Structure.ListGlobal` | `Request_GlobalStructure_List` → `Event_GlobalStructure_List` | `Id(0)` (all) or `Id(playfieldId)` | `GlobalStructureList` | Partial: V2 `Application.GetStructures(playfield, ...)` — loaded playfields only |
| `V1.Structure.Update` | `Request_GlobalStructure_Update` | `GlobalStructureInfo` | `Event_Ok` | None |
| `V1.Structure.Touch` | `Request_Structure_Touch` | `Id(entityId)` | `Event_Ok` | None — updates `lastVisitedTicks` server-side |
| `V1.Structure.BlockStats` | `Request_Structure_BlockStatistics` → `Event_Structure_BlockStatistics` | `Id(entityId)` | `IdStructureBlockInfo` | None |

**`GlobalStructureInfo` fields:** id, type, factionGroup, factionId, name, lastVisitedUTC, pos, rot, powered, fuel, cntDevices/Blocks/Triangles/Lights, classNr, dockedShips[], coreType, pilotId, PlayfieldName, Sector, SolarSystemName, SolarSystemCoord

**When to use V1 Structure:**
- Global structure list is the primary use: enumerate all structures across all playfields, including those on idle/unloaded playfields — V2 cannot see unloaded playfields
- `Request_GlobalStructure_List` is also how you find which playfield an entity is on without scanning each one

---

### Playfield

Server-side playfield management. V2 `IPlayfield` provides richer object-oriented access but only to the currently loaded local playfield.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Playfield.List` | `Request_Playfield_List` → `Event_Playfield_List` | _(none)_ | `PlayfieldList` | Partial: V2 `Application.GetAllPlayfields()` → `IPlayfieldDescr[]` |
| `V1.Playfield.Stats` | `Request_Playfield_Stats` → `Event_Playfield_Stats` | `PString(playfieldName)` | `PlayfieldStats` | None |
| `V1.Playfield.Load` | `Request_Load_Playfield` | `PlayfieldLoad` | `Event_Ok` | None — force-loads an idle playfield |
| `V1.Playfield.EntityList` | `Request_Playfield_Entity_List` → `Event_Playfield_Entity_List` | `PString(playfieldName)` | `PlayfieldEntityList` | Partial: V2 `IPlayfield.Entities` dict — loaded playfield only |

**When to use V1 Playfield:**
- `Request_Playfield_Entity_List` is useful for scanning entity lists cross-playfield without needing each PlayfieldServer instance to respond
- `Request_Load_Playfield` is V1-exclusive: force a playfield to load (wake idle playfield server)
- Playfield stats (memory, player count, entity counts) are V1-only

---

### Server / Admin

Dedicated server administration operations.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Server.Stats` | `Request_Dedi_Stats` → `Event_Dedi_Stats` | _(none)_ | `DediStats` | None |
| `V1.Server.ConsoleCommand` | `Request_ConsoleCommand` (fire-and-forget) | `PString(command)` | `{"Ok":true}` — no output captured | None |
| `V1.Server.BannedPlayers` | `Request_GetBannedPlayers` → `Event_BannedPlayers` | _(none)_ | `BannedPlayerData` | None |

**Confirmed console command syntax:**
- `ban <playerName> <duration>` — duration format: `1h`, `2h`, etc. (not `1hr`)
- `unban <playerName>`
- Ban entries carry `steam64Id` (ulong) and `dateTime` (long, raw .NET ticks — not a readable timestamp)

**When to use V1 Server:**
- Console commands let you invoke any server-side command from MQTT — the most general-purpose V1 escape hatch
- Server stats (connected players, uptime, FPS) are V1-exclusive

---

### Faction / Alliance

Server-wide faction and alliance graph. V2 exposes `FactionData` per-entity but has no equivalent for listing all factions or alliance relationships.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Faction.List` | `Request_Get_Factions` → `Event_Get_Factions` | _(none)_ | `FactionInfoList` | None |
| `V1.Faction.AlliancesAll` | `Request_AlliancesAll` → `Event_AlliancesAll` | _(none)_ | `AlliancesTable` | None |
| `V1.Faction.AlliancesByFaction` | `Request_AlliancesFaction` → `Event_AlliancesFaction` | `Id(factionId)` | `AlliancesFaction` | None |

**`FactionInfo` fields:** origin, factionId, name, abbrev

**When to use V1 Faction:**
- Building the faction graph (who is allied with whom) requires V1 — only available server-wide
- Checking whether two factions are at war/allied before triggering game events

---

### Messaging / UI

Server-side player notifications. V2 `IGui` and `IPda` work per-process; V1 can target any player server-wide.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Message.ToPlayer` | `Request_InGameMessage_SinglePlayer` | `IdMsgPrio(entityId, msg, prio)` | `Event_Ok` | Partial: V2 `IGui.ShowGameMessage` is local-process only |
| `V1.Message.ToAll` | `Request_InGameMessage_AllPlayers` | `IdMsgPrio(0, msg, prio)` | `Event_Ok` | None — broadcasts to all connected players |
| `V1.Message.ToFaction` | `Request_InGameMessage_Faction` | `IdMsgPrio(factionId, msg, prio)` | `Event_Ok` | None |
| `V1.Message.Dialog` | `Request_ShowDialog_SinglePlayer` → `Event_DialogButtonIndex` | `DialogBoxData` | `DialogBoxData` (with button result) | Partial: V2 `Application.ShowDialogBox` is client-side |
| _(subscribe)_ | `Event_ChatMessage` / `Event_ChatMessageEx` | — | `ChatInfo` / `ChatMsgData` | Partial: V2 `Application.ChatMessageSent` is local-process only |

**When to use V1 Messaging:**
- Sending a message or dialog to a player on any playfield without knowing which PlayfieldServer or Client they are on
- Broadcast to all players or entire factions requires V1

---

### Blueprint

Factory / blueprint operations.

| ESB Handler (proposed) | CmdId Request → Event | Request Data | Response Data | V2 Equivalent? |
|---|---|---|---|---|
| `V1.Blueprint.Finish` | `Request_Blueprint_Finish` | `Id(entityId)` | `Event_Ok` | None — instantly completes blueprint in player factory |
| `V1.Blueprint.Resources` | `Request_Blueprint_Resources` → `Event_Blueprint_Resources` | `BlueprintResources` | `BlueprintResources` | None |

---

## Passive Events (No Request Counterpart)

These CmdIds fire server-initiated events. In ESB they would be published as MQTT events rather than handled as requests.

| ESB Event Topic (proposed) | CmdId | Payload Type | Notes |
|---|---|---|---|
| `V1/E/Player.Connected` | `Event_Player_Connected` | `PlayerInfo` | Player joins the server |
| `V1/E/Player.Disconnected` | `Event_Player_Disconnected` | `PlayerInfo` | Player leaves the server |
| `V1/E/Player.DisconnectedWaiting` | `Event_Player_DisconnectedWaiting` | `PlayerInfo` | Player disconnected but server is holding state |
| `V1/E/Player.ChangedPlayfield` | `Event_Player_ChangedPlayfield` | `IdPlayfield` | Player transitions between playfields |
| `V1/E/Playfield.Loaded` | `Event_Playfield_Loaded` | `PString(name)` | Playfield server came online |
| `V1/E/Playfield.Unloaded` | `Event_Playfield_Unloaded` | `PString(name)` | Playfield server went idle |
| `V1/E/Faction.Changed` | `Event_Faction_Changed` | `FactionChangeInfo` | Faction membership or relationship changed |
| `V1/E/Statistics` | `Event_Statistics` | `StatisticsParam` | CoreRemoved/Added, PlayerDied, StructOnOff, StructDestroyed |
| `V1/E/Pda.StateChange` | `Event_PdaStateChange` | `PdaStateInfo` | Chapter activated/deactivated/completed |
| `V1/E/Trader.ItemSold` | `Event_TraderNPCItemSold` | `TraderNPCItemSoldInfo` | Trader NPC sale completed |
| `V1/E/GameEvent` | `Event_GameEvent` | `GameEventData` | General game event |

**Player lifecycle events** (`Connected`, `Disconnected`, `ChangedPlayfield`) are the primary reason to use V1 in a multiplayer context — V2 has no equivalent server-wide player lifecycle events.

---

## V1-Exclusive Capabilities Summary

These operations have no V2 equivalent at any scope:

| Capability | Handler Group | Why it matters |
|---|---|---|
| Player connect/disconnect events | Events | Track player sessions server-wide |
| Player inventory read/write for any player | Player | Modify other players' items from MQTT |
| Credits read/write for any player | Player | Economy management |
| Player item exchange UI | Player | Custom shop/trade UI via MQTT |
| Cross-playfield entity teleport | Entity | Move any entity to any playfield |
| Entity spawn (server-wide, with faction) | Entity | Spawn with full control from MQTT |
| Global structure list (unloaded playfields) | Structure | Server-wide asset inventory |
| Force-load playfield | Playfield | Wake idle playfield servers |
| Faction + alliance graph | Faction | Relationship queries |
| In-game message to all players / by faction | Messaging | Server broadcasts |
| Console command execution | Server | General-purpose server admin |
| Blueprint factory control | Blueprint | Economy scripting |

---

## Pattern: Request → Response

Every V1 request follows the same async pattern in `EmpyrionModBase`:

```csharp
// EmpyrionModBase wraps CmdId pairs as Task<T>:
var inventory = await _ctx.ModBase.Request_Player_GetInventory(new Id(entityId));
// → sends CmdId.Request_Player_GetInventory
// ← receives CmdId.Event_Player_Inventory as Inventory object
```

For raw CmdId access (when EmpyrionModBase has no wrapper):
```csharp
_ctx.ModBase.Game_Request(CmdId.Request_ConsoleCommand, seqNr, new PString("say hello"));
// handle via Game_Event(CmdId.Event_ConsoleCommand, seqNr, data)
```

On error, `Event_Error` fires instead of the expected event, carrying an `ErrorInfo` with an `ErrorType` enum value.

---

## Relationship to V2

The same handler class can implement both V1 and V2 paths for a unified ESB topic. The `V1.Player.GetInventory` handler already does this pattern: it calls `Request_Player_GetInventory` and returns an `Inventory` object.

A unified `Player.Inventory` handler (future) would:
1. If `EntityId` is omitted → use V2 `IPlayer.Bag/Toolbar` (local player, works in SP, no round-trip)
2. If `EntityId` is specified → use V1 `Request_Player_GetInventory` (any player, DedicatedServer only)

See [ApiLayers.md](ApiLayers.md) for the general V1/V2 routing model and the read-through cache pattern.
