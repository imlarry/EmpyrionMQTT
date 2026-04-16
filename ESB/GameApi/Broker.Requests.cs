using Eleon.Modding;
using System;
using System.Threading.Tasks;

// Typed async wrappers over SendRequestAsync for all V1 API calls.
// Use these in V1 topic handlers instead of calling SendRequestAsync directly.

namespace ESB.GameApi
{
    public partial class Broker
    {
        public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public async Task<PlayfieldList>       Request_Playfield_List()                               => await TaskTools.For(DefaultTimeout,  SendRequestAsync<PlayfieldList>(CmdId.Request_Playfield_List, null));
        public async Task<PlayfieldList>       Request_Playfield_List(Timeouts t)                     { try { return await TaskTools.For(Span(t), SendRequestAsync<PlayfieldList>(CmdId.Request_Playfield_List, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<PlayfieldStats>      Request_Playfield_Stats(PString arg)                   => await TaskTools.For(DefaultTimeout,  SendRequestAsync<PlayfieldStats>(CmdId.Request_Playfield_Stats, arg));
        public async Task<PlayfieldStats>      Request_Playfield_Stats(Timeouts t, PString arg)       { try { return await TaskTools.For(Span(t), SendRequestAsync<PlayfieldStats>(CmdId.Request_Playfield_Stats, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<DediStats>           Request_Dedi_Stats()                                   => await TaskTools.For(DefaultTimeout,  SendRequestAsync<DediStats>(CmdId.Request_Dedi_Stats, null));
        public async Task<DediStats>           Request_Dedi_Stats(Timeouts t)                         { try { return await TaskTools.For(Span(t), SendRequestAsync<DediStats>(CmdId.Request_Dedi_Stats, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<GlobalStructureList> Request_GlobalStructure_List()                         => await TaskTools.For(DefaultTimeout,  SendRequestAsync<GlobalStructureList>(CmdId.Request_GlobalStructure_List, null));
        public async Task<GlobalStructureList> Request_GlobalStructure_List(Timeouts t)               { try { return await TaskTools.For(Span(t), SendRequestAsync<GlobalStructureList>(CmdId.Request_GlobalStructure_List, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_GlobalStructure_Update(PString arg)             { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_GlobalStructure_Update, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Structure_Touch(Id arg)                         { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Structure_Touch, arg)); } catch (TaskCanceledException) { } }

        public async Task<IdStructureBlockInfo>Request_Structure_BlockStatistics(Id arg)              => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdStructureBlockInfo>(CmdId.Request_Structure_BlockStatistics, arg));
        public async Task<IdStructureBlockInfo>Request_Structure_BlockStatistics(Timeouts t, Id arg)  { try { return await TaskTools.For(Span(t), SendRequestAsync<IdStructureBlockInfo>(CmdId.Request_Structure_BlockStatistics, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<PlayerInfo>          Request_Player_Info(Id arg)                            => await TaskTools.For(DefaultTimeout,  SendRequestAsync<PlayerInfo>(CmdId.Request_Player_Info, arg));
        public async Task<PlayerInfo>          Request_Player_Info(Timeouts t, Id arg)                { try { return await TaskTools.For(Span(t), SendRequestAsync<PlayerInfo>(CmdId.Request_Player_Info, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<IdList>              Request_Player_List()                                  => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdList>(CmdId.Request_Player_List, null));
        public async Task<IdList>              Request_Player_List(Timeouts t)                        { try { return await TaskTools.For(Span(t), SendRequestAsync<IdList>(CmdId.Request_Player_List, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<Inventory>           Request_Player_GetInventory(Id arg)                    => await TaskTools.For(DefaultTimeout,  SendRequestAsync<Inventory>(CmdId.Request_Player_GetInventory, arg));
        public async Task<Inventory>           Request_Player_GetInventory(Timeouts t, Id arg)        { try { return await TaskTools.For(Span(t), SendRequestAsync<Inventory>(CmdId.Request_Player_GetInventory, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<Inventory>           Request_Player_SetInventory(Inventory arg)             => await TaskTools.For(DefaultTimeout,  SendRequestAsync<Inventory>(CmdId.Request_Player_SetInventory, arg));
        public async Task<Inventory>           Request_Player_SetInventory(Timeouts t, Inventory arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<Inventory>(CmdId.Request_Player_SetInventory, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Player_AddItem(IdItemStack arg)                 { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Player_AddItem, arg)); } catch (TaskCanceledException) { } }

        public async Task<IdCredits>           Request_Player_Credits(Id arg)                         => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdCredits>(CmdId.Request_Player_Credits, arg));
        public async Task<IdCredits>           Request_Player_Credits(Timeouts t, Id arg)             { try { return await TaskTools.For(Span(t), SendRequestAsync<IdCredits>(CmdId.Request_Player_Credits, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<IdCredits>           Request_Player_SetCredits(IdCredits arg)               => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdCredits>(CmdId.Request_Player_SetCredits, arg));
        public async Task<IdCredits>           Request_Player_SetCredits(Timeouts t, IdCredits arg)   { try { return await TaskTools.For(Span(t), SendRequestAsync<IdCredits>(CmdId.Request_Player_SetCredits, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<IdCredits>           Request_Player_AddCredits(IdCredits arg)               => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdCredits>(CmdId.Request_Player_AddCredits, arg));
        public async Task<IdCredits>           Request_Player_AddCredits(Timeouts t, IdCredits arg)   { try { return await TaskTools.For(Span(t), SendRequestAsync<IdCredits>(CmdId.Request_Player_AddCredits, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Blueprint_Finish(Id arg)                        { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Blueprint_Finish, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Blueprint_Resources(BlueprintResources arg)     { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Blueprint_Resources, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Player_ChangePlayerfield(IdPlayfieldPositionRotation arg) { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Player_ChangePlayerfield, arg)); } catch (TaskCanceledException) { } }

        public async Task<ItemExchangeInfo>    Request_Player_ItemExchange(ItemExchangeInfo arg)       => await TaskTools.For(DefaultTimeout,  SendRequestAsync<ItemExchangeInfo>(CmdId.Request_Player_ItemExchange, arg));
        public async Task<ItemExchangeInfo>    Request_Player_ItemExchange(Timeouts t, ItemExchangeInfo arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<ItemExchangeInfo>(CmdId.Request_Player_ItemExchange, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Player_SetPlayerInfo(PlayerInfoSet arg)          { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Player_SetPlayerInfo, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Entity_Teleport(IdPositionRotation arg)          { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_Teleport, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Entity_ChangePlayfield(IdPlayfieldPositionRotation arg) { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_ChangePlayfield, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Entity_Destroy(Id arg)                           { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_Destroy, arg)); } catch (TaskCanceledException) { } }

        public async Task<IdPositionRotation>  Request_Entity_PosAndRot(Id arg)                       => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdPositionRotation>(CmdId.Request_Entity_PosAndRot, arg));
        public async Task<IdPositionRotation>  Request_Entity_PosAndRot(Timeouts t, Id arg)           { try { return await TaskTools.For(Span(t), SendRequestAsync<IdPositionRotation>(CmdId.Request_Entity_PosAndRot, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Entity_Spawn(EntitySpawnInfo arg)               { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_Spawn, arg)); } catch (TaskCanceledException) { } }

        public async Task<FactionInfoList>     Request_Get_Factions(Id arg)                           => await TaskTools.For(DefaultTimeout,  SendRequestAsync<FactionInfoList>(CmdId.Request_Get_Factions, arg));
        public async Task<FactionInfoList>     Request_Get_Factions(Timeouts t, Id arg)               { try { return await TaskTools.For(Span(t), SendRequestAsync<FactionInfoList>(CmdId.Request_Get_Factions, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<Id>                  Request_NewEntityId()                                  => await TaskTools.For(DefaultTimeout,  SendRequestAsync<Id>(CmdId.Request_NewEntityId, null));
        public async Task<Id>                  Request_NewEntityId(Timeouts t)                        { try { return await TaskTools.For(Span(t), SendRequestAsync<Id>(CmdId.Request_NewEntityId, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<AlliancesTable>      Request_AlliancesAll()                                 => await TaskTools.For(DefaultTimeout,  SendRequestAsync<AlliancesTable>(CmdId.Request_AlliancesAll, null));
        public async Task<AlliancesTable>      Request_AlliancesAll(Timeouts t)                       { try { return await TaskTools.For(Span(t), SendRequestAsync<AlliancesTable>(CmdId.Request_AlliancesAll, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<AlliancesFaction>    Request_AlliancesFaction(AlliancesFaction arg)         => await TaskTools.For(DefaultTimeout,  SendRequestAsync<AlliancesFaction>(CmdId.Request_AlliancesFaction, arg));
        public async Task<AlliancesFaction>    Request_AlliancesFaction(Timeouts t, AlliancesFaction arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<AlliancesFaction>(CmdId.Request_AlliancesFaction, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Load_Playfield(PlayfieldLoad arg)               { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Load_Playfield, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_ConsoleCommand(PString arg)                     { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_ConsoleCommand, arg)); } catch (TaskCanceledException) { } }

        public async Task<IdList>              Request_GetBannedPlayers()                             => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdList>(CmdId.Request_GetBannedPlayers, null));
        public async Task<IdList>              Request_GetBannedPlayers(Timeouts t)                   { try { return await TaskTools.For(Span(t), SendRequestAsync<IdList>(CmdId.Request_GetBannedPlayers, null)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_InGameMessage_SinglePlayer(IdMsgPrio arg)       { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_InGameMessage_SinglePlayer, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_InGameMessage_AllPlayers(IdMsgPrio arg)         { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_InGameMessage_AllPlayers, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_InGameMessage_Faction(IdMsgPrio arg)            { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_InGameMessage_Faction, arg)); } catch (TaskCanceledException) { } }

        public async Task<IdAndIntValue>       Request_ShowDialog_SinglePlayer(DialogBoxData arg)     => await TaskTools.For(DefaultTimeout,  SendRequestAsync<IdAndIntValue>(CmdId.Request_ShowDialog_SinglePlayer, arg));
        public async Task<IdAndIntValue>       Request_ShowDialog_SinglePlayer(Timeouts t, DialogBoxData arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<IdAndIntValue>(CmdId.Request_ShowDialog_SinglePlayer, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<Inventory>           Request_Player_GetAndRemoveInventory(Id arg)           => await TaskTools.For(DefaultTimeout,  SendRequestAsync<Inventory>(CmdId.Request_Player_GetAndRemoveInventory, arg));
        public async Task<Inventory>           Request_Player_GetAndRemoveInventory(Timeouts t, Id arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<Inventory>(CmdId.Request_Player_GetAndRemoveInventory, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task<PlayfieldEntityList> Request_Playfield_Entity_List(PString arg)             => await TaskTools.For(DefaultTimeout,  SendRequestAsync<PlayfieldEntityList>(CmdId.Request_Playfield_Entity_List, arg));
        public async Task<PlayfieldEntityList> Request_Playfield_Entity_List(Timeouts t, PString arg) { try { return await TaskTools.For(Span(t), SendRequestAsync<PlayfieldEntityList>(CmdId.Request_Playfield_Entity_List, arg)); } catch (TaskCanceledException) { if ((int)t > 0) throw; return default; } }

        public async Task                      Request_Entity_Destroy2(IdPlayfield arg)               { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_Destroy2, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Entity_Export(EntityExportInfo arg)             { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_Export, arg)); } catch (TaskCanceledException) { } }
        public async Task                      Request_Entity_SetName(IdPlayfieldName arg)             { try { await TaskTools.For(TimeSpan.Zero, SendRequestAsync<bool>(CmdId.Request_Entity_SetName, arg)); } catch (TaskCanceledException) { } }

        private static TimeSpan Span(Timeouts t) => TimeSpan.FromSeconds((int)t);
    }
}
