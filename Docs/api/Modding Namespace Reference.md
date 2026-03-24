# Eleon.Modding Namespace Reference


## Classes

- **`interface ModInterface`**
- **`interface ModGameAPI`**
- **`class EntitySpawnInfo`**
- **`struct FactionInfo`**
- **`class FactionInfoList`**
- **`class StatisticsParam`**
- **`class Id`**
- **`class IdAndIntValue`**
- **`class IdMsgPrio`**
- **`class DialogBoxData`**
- **`class PlayfieldList`**
- **`class FactionChangeInfo`**
- **`struct GlobalStructureInfo`**
- **`class GlobalStructureList`**
- **`class IdPlayfield`**
- **`class IdPlayfieldName`**
- **`class PlayfieldLoad`**
- **`class PlayfieldStats`**
- **`class DediStats`**
- **`class PString`**
- **`class IdList`**
- **`class IdCredits`**
- **`struct PVector3`**
- **`class ItemExchangeInfo`**
- **`class IdPlayfieldPositionRotation`**
- **`class IdPositionRotation`**
- **`class IdItemStack`**
- **`class ErrorInfo`**
- **`class PlayerInfoSet`**
- **`class PlayerInfo`**
- **`class Inventory`**
- **`struct ItemStack`**
- **`class BlueprintResources`**
- **`class ChatInfo`**
- **`class ChatMsgData`**
- **`class AlliancesTable`**
- **`class AlliancesFaction`**
- **`class IdStructureBlockInfo`**
- **`class BannedPlayerData`**
- **`class TraderNPCItemSoldInfo`**
- **`class EntityInfo`**
- **`class PlayfieldEntityList`**
- **`class EntityExportInfo`**
- **`class ConsoleCommandInfo`**
- **`struct PdaStateInfo`**
- **`struct GameEventData`**
- **`interface IMod`**
  - New mods have to implement this interface - otherwise we cannot provide an IModApi instance
- **`interface IEntity`**
- **`interface ISoundPlayer`**
- **`interface IPlayer`**
- **`interface IDevice`**
- **`interface ILcd`**
- **`interface ITeleporter`**
- **`interface IPortal`**
- **`interface ILight`**
- **`interface IContainer`**
  - A container (e.g. a cargo box or a fridge) is a storage unit that can contain stacks of items
- **`interface IBlock`**
- **`interface IDevicePosList`**
- **`interface IStructureTank`**
  - Tank that accumulates all material of a specific type (in opposite to standard containers with discrete material stacks)
- **`interface IStructure`**
- **`interface IPlayfield`**
- **`interface IGui`**
- **`interface IPda`**
- **`interface INetwork`**
  - Inter-mod communication functions. Use to send data from one mod to another and be notified on received data.
- **`interface IScript`**
- **`class ScriptDomain`**
- **`class ScriptType`**
- **`class ScriptInstance`**
- **`interface IPlayfieldDescr`**
- **`interface IApplication`**
- **`interface IModApi`**
- **`class ModConsts`**
- **`struct SenderSignal`**
  - Sender signal data
- **`struct SignalFunction`**
  - Setup for a signal controlled block function
- **`struct TeleporterData`**
  - Target data of a Teleporter or Portal
- **`struct VectorInt3`**
- **`class DialogConfig`**
  - Config data for dialog box - title and body texts are TMPro elements (can use rich text tags like new LCD signs)
- **`struct PlayerData`**
  - Some core data about a player

## Enumerations

- **`enum class CmdId { 
  Event_Playfield_Loaded
, Event_Playfield_Unloaded
, Request_Playfield_List
, Event_Playfield_List
, 
  Request_Playfield_Stats
, Event_Playfield_Stats
, Request_Dedi_Stats
, Event_Dedi_Stats
, 
  Request_GlobalStructure_List
, Request_GlobalStructure_Update
, Event_GlobalStructure_List
, Request_Structure_Touch
, 
  Request_Structure_BlockStatistics
, Event_Structure_BlockStatistics
, Event_Player_Connected
, Event_Player_Disconnected
, 
  Event_Player_ChangedPlayfield
, Request_Player_Info
, Event_Player_Info
, Request_Player_List
, 
  Event_Player_List
, Request_Player_GetInventory
, Request_Player_SetInventory
, Event_Player_Inventory
, 
  Request_Player_AddItem
, Request_Player_Credits
, Request_Player_SetCredits
, Request_Player_AddCredits
, 
  Event_Player_Credits
, Request_Blueprint_Finish
, Request_Blueprint_Resources
, Request_Player_ChangePlayerfield
, 
  Request_Player_ItemExchange
, Event_Player_ItemExchange
, Request_Player_SetPlayerInfo
, Event_Player_DisconnectedWaiting
, 
  Request_Entity_Teleport
, Request_Entity_ChangePlayfield
, Request_Entity_Destroy
, Request_Entity_PosAndRot
, 
  Event_Entity_PosAndRot
, Event_Faction_Changed
, Request_Entity_Spawn
, Request_Get_Factions
, 
  Event_Get_Factions
, Event_Statistics
, Request_NewEntityId
, Event_NewEntityId
, 
  Request_AlliancesAll
, Event_AlliancesAll
, Request_AlliancesFaction
, Event_AlliancesFaction
, 
  Request_Load_Playfield
, Event_ChatMessage
, Event_ChatMessageEx
, Request_ConsoleCommand
, 
  Request_GetBannedPlayers
, Event_BannedPlayers
, Request_InGameMessage_SinglePlayer
, Request_InGameMessage_AllPlayers
, 
  Request_InGameMessage_Faction
, Request_ShowDialog_SinglePlayer
, Event_DialogButtonIndex
, Event_Ok
, 
  Event_Error
, Event_TraderNPCItemSold
, Request_Player_GetAndRemoveInventory
, Event_Player_GetAndRemoveInventory
, 
  Request_Playfield_Entity_List
, Event_Playfield_Entity_List
, Request_Entity_Destroy2
, Request_Entity_Export
, 
  Event_ConsoleCommand
, Request_Entity_SetName
, Event_PdaStateChange
, Event_GameEvent

 }`**
- **`enum class StatisticsType { 
  CoreRemoved
, CoreAdded
, PlayerDied
, StructOnOff
, 
  StructDestroyed

 }`**
- **`enum class PdaStateChange { Undefined
, ChapterActivated
, ChapterDeactivated
, ChapterCompleted
 }`**
- **`enum class ErrorType : byte { 
  None
, MissingParameter
, PlayerIdNotFound
, PlayfieldOfPlayerNotFound
, 
  PlayfieldConnectionNotFound
, EntityIdNotFound
, CouldNotAddItemToInventory
, CouldNotRemoveItemFromInventory
, 
  BlueprintError
, NotEnoughCredits
, EntityTypeNotSupported
, NoIdlePlayfieldFound
, 
  PlayfieldCannotBeLoaded
, PlayfieldAlreadyLoaded
, CommandNotImplemented
, IOError
, 
  EntityNotLocalToPlayfield

 }`**
- **`enum class SenderType : byte { 
  Unknown
, Player
, ServerPrio
, ServerInfo
, 
  ServerForward
, System

 }`**
- **`enum class MsgChannel : byte { 
  Global
, Faction
, Alliance
, SinglePlayer
, 
  Server

 }`**
- **`enum class AppFolder { 
  Root
, Content
, SaveGame
, Mod
, 
  ActiveScenario
, Cache
, Dedicated

 }`**
- **`enum class GameState { NotRunning
, Loading
, Running
 }`**
- **`enum class SoundPlayMode { 
  OneShot
, LoopStart
, Loop
, LoopEnd
, 
  LoopStop

 }`**
- **`enum class ApplicationMode { SinglePlayer
, Client
, DedicatedServer
, PlayfieldServer
 }`**
- **`enum class ModApiDialogPosition { 
  Left
, Mid
, Right
, Positive = Left
, 
  Cancel = Right

 }`**
- **`enum class ModApiDialogButtons { 
  Ok
, Cancel
, Quit
, Ok_Cancel
, 
  Set_Cancel
, Yes_No
, Skip_LetsGo
, LetsGo
, 
  Accept_Decline
, None

 }`**

## Functions

- **`delegate void SignalChangedEventHandler (string name, bool newState, int triggeringEntityId)`**
- **`delegate void EntityDelegate (IEntity entity)`**
- **`delegate void PlayfieldDelegate (IPlayfield playfield)`**
- **`delegate void LockResultCallback (int structureId, VectorInt3 posInStruct, bool success)`**
- **`delegate void DialogActionHandler (int buttonIdx, string linkId, string inputContent, int playerId, int customValue)`**
  - buttonIdx: index of pressed button (0 = left, 1 = mid, 2 = right) or -1 if it wasn't a button click linkText: text in the link tag, e.g. "buy" in "<link="buy">Buy item</link>" or empty string if it wasn't a link click inputContent: content of input field playerId: which player acted customValue: for request / answer matching
- **`delegate void ModDataReceivedDelegate (string sender, string playfieldName, byte[] data)`**
- **`delegate void PlayerDataReceivedDelegate (string sender, int playerEntityId, byte[] data)`**
- **`delegate void UpdateDelegate ()`**
- **`delegate void GamEnteredEventHandler (bool hasEntered)`**
- **`delegate void ChatMessageSentEventHandler (MessageData chatMsgData)`**
- **`delegate void GameEventDelegate (GameEventType type, object arg1=null, object arg2=null, object arg3=null, object arg4=null, object arg5=null)`**
