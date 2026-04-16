using Eleon.Modding;
using System;
using System.Collections.Generic;

// Typed event subscriptions for V1 game events.
// Subscribe directly to these instead of filtering API_Message_Received by CmdId.

namespace ESB.GameApi
{
    public partial class Broker
    {
        private Dictionary<CmdId, Delegate> eventTable = new Dictionary<CmdId, Delegate>();

        public event Action<PlayfieldLoad>        Event_Playfield_Loaded           { add { Subscribe(CmdId.Event_Playfield_Loaded,           value); } remove { Unsubscribe(CmdId.Event_Playfield_Loaded,           value); } }
        public event Action<PlayfieldLoad>        Event_Playfield_Unloaded         { add { Subscribe(CmdId.Event_Playfield_Unloaded,         value); } remove { Unsubscribe(CmdId.Event_Playfield_Unloaded,         value); } }
        public event Action<Id>                   Event_Player_Connected           { add { Subscribe(CmdId.Event_Player_Connected,           value); } remove { Unsubscribe(CmdId.Event_Player_Connected,           value); } }
        public event Action<Id>                   Event_Player_Disconnected        { add { Subscribe(CmdId.Event_Player_Disconnected,        value); } remove { Unsubscribe(CmdId.Event_Player_Disconnected,        value); } }
        public event Action<IdPlayfield>          Event_Player_ChangedPlayfield    { add { Subscribe(CmdId.Event_Player_ChangedPlayfield,    value); } remove { Unsubscribe(CmdId.Event_Player_ChangedPlayfield,    value); } }
        public event Action<Id>                   Event_Player_DisconnectedWaiting { add { Subscribe(CmdId.Event_Player_DisconnectedWaiting, value); } remove { Unsubscribe(CmdId.Event_Player_DisconnectedWaiting, value); } }
        public event Action<FactionChangeInfo>    Event_Faction_Changed            { add { Subscribe(CmdId.Event_Faction_Changed,            value); } remove { Unsubscribe(CmdId.Event_Faction_Changed,            value); } }
        public event Action<StatisticsParam>      Event_Statistics                 { add { Subscribe(CmdId.Event_Statistics,                 value); } remove { Unsubscribe(CmdId.Event_Statistics,                 value); } }
        public event Action<ChatInfo>             Event_ChatMessage                { add { Subscribe(CmdId.Event_ChatMessage,                value); } remove { Unsubscribe(CmdId.Event_ChatMessage,                value); } }
        public event Action<TraderNPCItemSoldInfo>Event_TraderNPCItemSold          { add { Subscribe(CmdId.Event_TraderNPCItemSold,          value); } remove { Unsubscribe(CmdId.Event_TraderNPCItemSold,          value); } }
        public event Action<ConsoleCommandInfo>   Event_ConsoleCommand             { add { Subscribe(CmdId.Event_ConsoleCommand,             value); } remove { Unsubscribe(CmdId.Event_ConsoleCommand,             value); } }
        public event Action<PdaStateInfo>         Event_PdaStateChange             { add { Subscribe(CmdId.Event_PdaStateChange,             value); } remove { Unsubscribe(CmdId.Event_PdaStateChange,             value); } }
        public event Action<GameEventData>        Event_GameEvent                  { add { Subscribe(CmdId.Event_GameEvent,                  value); } remove { Unsubscribe(CmdId.Event_GameEvent,                  value); } }
        public event Action<AlliancesTable>       Event_AlliancesAll               { add { Subscribe(CmdId.Event_AlliancesAll,               value); } remove { Unsubscribe(CmdId.Event_AlliancesAll,               value); } }
        public event Action<AlliancesFaction>     Event_AlliancesFaction           { add { Subscribe(CmdId.Event_AlliancesFaction,           value); } remove { Unsubscribe(CmdId.Event_AlliancesFaction,           value); } }
        public event Action<IdList>               Event_BannedPlayers              { add { Subscribe(CmdId.Event_BannedPlayers,              value); } remove { Unsubscribe(CmdId.Event_BannedPlayers,              value); } }
        public event Action<DediStats>            Event_Dedi_Stats                 { add { Subscribe(CmdId.Event_Dedi_Stats,                 value); } remove { Unsubscribe(CmdId.Event_Dedi_Stats,                 value); } }
        public event Action<IdPositionRotation>   Event_Entity_PosAndRot           { add { Subscribe(CmdId.Event_Entity_PosAndRot,           value); } remove { Unsubscribe(CmdId.Event_Entity_PosAndRot,           value); } }
        public event Action<FactionInfoList>      Event_Get_Factions               { add { Subscribe(CmdId.Event_Get_Factions,               value); } remove { Unsubscribe(CmdId.Event_Get_Factions,               value); } }
        public event Action<GlobalStructureList>  Event_GlobalStructure_List       { add { Subscribe(CmdId.Event_GlobalStructure_List,       value); } remove { Unsubscribe(CmdId.Event_GlobalStructure_List,       value); } }
        public event Action<Id>                   Event_NewEntityId                { add { Subscribe(CmdId.Event_NewEntityId,                value); } remove { Unsubscribe(CmdId.Event_NewEntityId,                value); } }
        public event Action                       Event_Ok                         { add { Subscribe(CmdId.Event_Ok,                         value); } remove { Unsubscribe(CmdId.Event_Ok,                         value); } }
        public event Action<IdCredits>            Event_Player_Credits             { add { Subscribe(CmdId.Event_Player_Credits,             value); } remove { Unsubscribe(CmdId.Event_Player_Credits,             value); } }
        public event Action<Inventory>            Event_Player_GetAndRemoveInventory { add { Subscribe(CmdId.Event_Player_GetAndRemoveInventory, value); } remove { Unsubscribe(CmdId.Event_Player_GetAndRemoveInventory, value); } }
        public event Action<PlayerInfo>           Event_Player_Info                { add { Subscribe(CmdId.Event_Player_Info,                value); } remove { Unsubscribe(CmdId.Event_Player_Info,                value); } }
        public event Action<Inventory>            Event_Player_Inventory           { add { Subscribe(CmdId.Event_Player_Inventory,           value); } remove { Unsubscribe(CmdId.Event_Player_Inventory,           value); } }
        public event Action<ItemExchangeInfo>     Event_Player_ItemExchange        { add { Subscribe(CmdId.Event_Player_ItemExchange,        value); } remove { Unsubscribe(CmdId.Event_Player_ItemExchange,        value); } }
        public event Action<IdList>               Event_Player_List                { add { Subscribe(CmdId.Event_Player_List,                value); } remove { Unsubscribe(CmdId.Event_Player_List,                value); } }
        public event Action<PlayfieldEntityList>  Event_Playfield_Entity_List      { add { Subscribe(CmdId.Event_Playfield_Entity_List,      value); } remove { Unsubscribe(CmdId.Event_Playfield_Entity_List,      value); } }
        public event Action<PlayfieldList>        Event_Playfield_List             { add { Subscribe(CmdId.Event_Playfield_List,             value); } remove { Unsubscribe(CmdId.Event_Playfield_List,             value); } }
        public event Action<PlayfieldStats>       Event_Playfield_Stats            { add { Subscribe(CmdId.Event_Playfield_Stats,            value); } remove { Unsubscribe(CmdId.Event_Playfield_Stats,            value); } }
        public event Action<IdStructureBlockInfo> Event_Structure_BlockStatistics  { add { Subscribe(CmdId.Event_Structure_BlockStatistics,  value); } remove { Unsubscribe(CmdId.Event_Structure_BlockStatistics,  value); } }
        public event Action<IdAndIntValue>        Event_DialogButtonIndex          { add { Subscribe(CmdId.Event_DialogButtonIndex,          value); } remove { Unsubscribe(CmdId.Event_DialogButtonIndex,          value); } }
        public event Action<ChatMsgData>          Event_ChatMessageEx              { add { Subscribe(CmdId.Event_ChatMessageEx,              value); } remove { Unsubscribe(CmdId.Event_ChatMessageEx,              value); } }

        private void Subscribe<T>(CmdId cmdId, Action<T> handler)
        {
            lock (eventTable)
                eventTable[cmdId] = eventTable.ContainsKey(cmdId)
                    ? (Action<T>)eventTable[cmdId] + handler : handler;
        }

        private void Subscribe(CmdId cmdId, Action handler)
        {
            lock (eventTable)
                eventTable[cmdId] = eventTable.ContainsKey(cmdId)
                    ? (Action)eventTable[cmdId] + handler : handler;
        }

        private void Unsubscribe<T>(CmdId cmdId, Action<T> handler)
        {
            lock (eventTable)
                eventTable[cmdId] = eventTable.ContainsKey(cmdId)
                    ? (Action<T>)eventTable[cmdId] - handler : handler;
        }

        private void Unsubscribe(CmdId cmdId, Action handler)
        {
            lock (eventTable)
                eventTable[cmdId] = eventTable.ContainsKey(cmdId)
                    ? (Action)eventTable[cmdId] - handler : handler;
        }
    }
}
