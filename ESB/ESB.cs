using System;
using System.Collections.Generic;
using Eleon;
using Eleon.Modding;
using ESBMessaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// EmpyrionMQTT .. mod for MQTT integration with Empyrion Galactic Survival

namespace ESBGameMod
{
    // context for this instance of mod
    public class ContextData
    {
        public IModApi ModApi { get; set; }
        public ModGameAPI ModLegacy { get; set; }
        public Messenger Messenger { get; set; }
        public BusManager BusManager { get; set; }
        public Dictionary<string, IPlayfield> LoadedPlayfield { get; set; }
        public Dictionary<int, IEntity> LoadedEntity { get; set; }
    }
    public class EmpyrionServiceBus : IMod, ModInterface
    {
        public ContextData CTX { get; set; } = new ContextData();

        // *** IModApi required Init interfaces **********************
        public async void Init(IModApi modApi)
        {
            CTX.ModApi = modApi;
            CTX.Messenger = new Messenger();
            CTX.BusManager = new BusManager();
            CTX.LoadedPlayfield = new Dictionary<string, IPlayfield>();
            CTX.LoadedEntity = new Dictionary<int, IEntity>();
            modApi.Log("ESB starting");

            // create mqtt client and open a channel to broker
            var applicationId = modApi.Application.Mode.ToString();
            await CTX.Messenger.ConnectAsync(CTX, applicationId, "localhost");

            // initialize subscriptions & dll plugins
            CTX.BusManager.Initialize(CTX);            

            // enable event handlers
            EnableEventHandlers(CTX.ModApi);
        }

        // *** IModApi required Shutdown interfaces ******************
        public async void Shutdown()
        {
            CTX.ModApi.Log("ESB exiting");

            DisableEventHandlers(CTX.ModApi);

            var now = DateTime.Today.ToString("s");
            JObject json = new JObject(
                    new JProperty("ClientId", CTX.Messenger.ClientId()),
                    new JProperty("DisconnectedAt", now)
                    );
            await CTX.Messenger.SendAsync("ModApi.Shutdown/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }


        // *** ModInterface required Game_Start interfaces ******************
        public void Game_Start(ModGameAPI dediAPI)
        {
            CTX.ModLegacy = dediAPI;
        }

        // *** ModInterface required Game_Update interfaces ******************
        public void Game_Update() { }

        // *** ModInterface required Game_Exit interfaces ******************
        public void Game_Exit() { }

        // *** ModInterface required Game_Event interfaces ******************
        public void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            switch (eventId)
            {
                case CmdId.Event_GlobalStructure_List:
                    //if (seqNr == WarpGate.WARPGATE_GSI)
                    //    WarpGateManager.OnGSL(this, (GlobalStructureList)data);
                    break;
                case CmdId.Event_Playfield_Loaded:
                    //WarpGateManager.OnPlayfieldLoaded((data as PlayfieldLoad).playfield);
                    //WarpManager.OnPlayfieldLoaded(this, (data as PlayfieldLoad).playfield);
                    break;
                case CmdId.Event_Playfield_Unloaded:
                    //WarpGateManager.OnPlayfieldUnloaded((data as PlayfieldLoad).playfield);
                    break;
                case CmdId.Event_Player_Info:
                    //if (seqNr == WarpGate.WARPGATE_PLAYERINFO_ID)
                    //    WarpGateManager.OnPlayerInfo(this, (PlayerInfo)data);
                    break;
            }
        }
        // *** activate callbacks TODO: configure specifics in ESB_Info config file

        private void EnableEventHandlers(IModApi modApi)
        {
            modApi.GameEvent += GameEventHandler;
            modApi.Application.Update += UpdateHandler;
            modApi.Application.ChatMessageSent += ChatMessageSentHandler;
            modApi.Application.GameEntered += GameEnteredHandler;
            modApi.Application.OnPlayfieldLoaded += OnPlayfieldLoadedHandler;
            modApi.Application.OnPlayfieldUnloading += OnPlayfieldUnloadingHandler;
        }

        private void DisableEventHandlers(IModApi modApi)
        {
            modApi.GameEvent -= GameEventHandler;
            modApi.Application.Update -= UpdateHandler;
            modApi.Application.ChatMessageSent -= ChatMessageSentHandler;
            modApi.Application.GameEntered -= GameEnteredHandler;
            modApi.Application.OnPlayfieldLoaded -= OnPlayfieldLoadedHandler;
            modApi.Application.OnPlayfieldUnloading -= OnPlayfieldUnloadingHandler;
        }

        // *********************** event handlers *********************** 

        async void GameEventHandler(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null)
        {
            if (type == GameEventType.WindowClosed || type == GameEventType.WindowOpened || type == GameEventType.InventoryContains 
                || type == GameEventType.InventoryContainsCountOfItem || type == GameEventType.HoldingItem || type == GameEventType.PlayerStatChanged) { return; }  // temp reduction in noise
            try
            {
                if (arg1 != null) arg1 = arg1.ToString();
                if (arg2 != null) arg2 = arg2.ToString();
                if (arg3 != null) arg3 = arg3.ToString();
                if (arg4 != null) arg4 = arg4.ToString();
                if (arg5 != null) arg5 = arg5.ToString();
                JObject json = new JObject(
                        new JProperty("Type", type.ToString()),
                        new JProperty("Arg1", arg1),
                        new JProperty("Arg2", arg2),
                        new JProperty("Arg3", arg3),
                        new JProperty("Arg4", arg4),
                        new JProperty("Arg5", arg5)
                        );
                await CTX.Messenger.SendAsync("ModApi.GameEvent." + type.ToString() + "/E", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                JObject json = new JObject(
                        new JProperty("ErrorOnType", type.ToString()),
                        new JProperty("Error", ex.Message)
                        );
                await CTX.Messenger.SendAsync("ModApi.GameEvent/X", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        private void UpdateHandler()
        {
            try
            {
                //WarpGateManager.OnUpdate(this);
            }
            catch (Exception ex)
            {
                CTX.ModApi.Log($"Exception - {ex.Message}");
            }
        }

        async void ChatMessageSentHandler(MessageData chatMsgData)
        {
            JObject json = new JObject(
                    new JProperty("SenderEntityId", chatMsgData.SenderEntityId),
                    new JProperty("SenderType", chatMsgData.SenderType.ToString()),
                    new JProperty("SenderNameOverride", chatMsgData.SenderNameOverride),
                    new JProperty("SenderFaction", null),
                    new JProperty("RecipientEntityId", chatMsgData.RecipientEntityId),
                    new JProperty("RecipientFaction", null),
                    new JProperty("GameTime", chatMsgData.GameTime),
                    new JProperty("IsTextLocaKey", chatMsgData.IsTextLocaKey),
                    new JProperty("Arg1", chatMsgData.Arg1),
                    new JProperty("Arg2", chatMsgData.Arg2),
                    new JProperty("Channel", chatMsgData.Channel.ToString()),
                    new JProperty("Text", chatMsgData.Text)
                    );
            await CTX.Messenger.SendAsync("ModApi.Application.ChatMessageSent/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void GameEnteredHandler(bool hasEntered)
        {
            JObject json = new JObject(new JProperty("HasEntered", hasEntered));
            await CTX.Messenger.SendAsync("ModApi.Application.GameEntered/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnPlayfieldLoadedHandler(IPlayfield playfield)
        {
            CTX.LoadedPlayfield.Add(playfield.Name, playfield); 
            playfield.OnEntityLoaded += OnEntityLoaded;
            playfield.OnEntityUnloaded += OnEntityUnloaded;
            JObject json = new JObject(
                    new JProperty("Name", playfield.Name),
                    new JProperty("PlayfieldType", playfield.PlayfieldType.ToString()),
                    new JProperty("PlanetType", playfield.PlanetType.ToString()),
                    new JProperty("PlanetClass", playfield.PlanetClass.ToString()),
                    new JProperty("SolarSystemName", playfield.SolarSystemName),
                    new JProperty("SolarSystemCoordinates", playfield.SolarSystemCoordinates.ToString()),
                    new JProperty("IsPvP", playfield.IsPvP.ToString())
                    );
            await CTX.Messenger.SendAsync("ModApi.Application.OnPlayfieldLoaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnPlayfieldUnloadingHandler(IPlayfield playfield)
        {
            CTX.LoadedPlayfield.Remove(playfield.Name);
            playfield.OnEntityLoaded -= OnEntityLoaded;
            playfield.OnEntityUnloaded -= OnEntityUnloaded;
            JObject json = new JObject(new JProperty("Name", playfield.Name));
            await CTX.Messenger.SendAsync("ModApi.Application.OnPlayfieldUnloading/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnEntityLoaded(IEntity entity)
        {
            CTX.LoadedEntity.Add(entity.Id, entity);
            JObject json = new JObject(
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name),
                    new JProperty("Faction", null),
                    new JProperty("Position", null),
                    new JProperty("Forward", null),
                    new JProperty("Rotation", null),
                    new JProperty("IsLocal", entity.IsLocal),
                    new JProperty("IsProxy", entity.IsProxy),
                    new JProperty("IsPoi", entity.IsPoi),
                    new JProperty("BelongsTo", entity.BelongsTo),
                    new JProperty("DockedTo", entity.DockedTo),
                    new JProperty("Type", entity.Type),
                    new JProperty("Structure", null)
                    );
            await CTX.Messenger.SendAsync("ModApi.Playfield.EntityLoaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnEntityUnloaded(IEntity entity)
        {
            CTX.LoadedEntity.Remove(entity.Id);
            JObject json = new JObject(
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name),
                    new JProperty("Type", entity.Type)
                    );
            await CTX.Messenger.SendAsync("ModApi.Playfield.OnEntityUnloaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

    }
}