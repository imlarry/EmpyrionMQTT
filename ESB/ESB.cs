using Eleon;
using Eleon.Modding;
using EmpyrionNetAPIAccess;
using ESBMessaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// EmpyrionMQTT .. mod for MQTT integration with Empyrion Galactic Survival

namespace ESBGameMod
{
    // context extensions for a mod
    public class ContextData : BaseContextData
    {
        public InitManager InitManager { get; set; }
        public IModApi ModApi { get; set; }
        // TODO: add ModGameAPI (required by dedi) & look at integration of ASTIC wrapper lib 
        // public ModGameAPI ModLegacy { get; set; }
        public List<KeyValuePair<string, IPlayfield>> LoadedPlayfield { get; set; }
        public List<KeyValuePair<int, IEntity>> LoadedEntity { get; set; }
    }
    /*   
    FETCH cached interface example:
    var akuaPlayfield = LoadedPlayfield.FirstOrDefault(pf => pf.Key == "Akua").Value;
    */

    public class EmpyrionServiceBus : EmpyrionModBase, IMod, ModInterface
    {
        public override void Initialize(ModGameAPI dediAPI)
        {
        }

        public ContextData CTX { get; set; } = new ContextData()
        {
            // prealloc to expected max + safety, use TrimExcess to limit to curcount, use Capacity to manually enlarge/shrink, default entry n+1 behavior is alloc n*2 and copy
            LoadedEntity = new List<KeyValuePair<int, IEntity>>(100)
        };

        // *** IModApi required Init interfaces **********************
        public async void Init(IModApi modApi)
        {
            CTX.ModApi = modApi;
            CTX.Messenger = new Messenger();
            CTX.InitManager = new InitManager();
            CTX.ModApi.Log("ESB starting");

            // create mqtt client and open a channel to broker
            var applicationId = modApi.Application.Mode.ToString();
            await CTX.Messenger.ConnectAsync(CTX, applicationId, "localhost");

            // initialize subscriptions & dll plugins
            CTX.InitManager.Initialize(CTX);

            // enable event handlers
            EnableEventHandlers(CTX.ModApi);
        }

        // *** IModApi required Shutdown interfaces ******************
        public async void Shutdown()
        {
            CTX.ModApi.Log("ESB exiting");

            DisableEventHandlers(CTX.ModApi);

            var now = DateTime.Now.ToString("s");
            JObject json = new JObject(
                    new JProperty("ClientId", CTX.Messenger.ClientId()),
                    new JProperty("DisconnectedAt", now)
                    );
            await CTX.Messenger.SendAsync("ModApi.Shutdown/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // *** activate callbacks

        private void EnableEventHandlers(IModApi modApi)
        {
            modApi.GameEvent += GameEventHandler;
            modApi.Application.ChatMessageSent += ChatMessageSentHandler;
            modApi.Application.GameEntered += GameEnteredHandler;
            modApi.Application.OnPlayfieldLoaded += OnPlayfieldLoadedHandler;
            modApi.Application.OnPlayfieldUnloading += OnPlayfieldUnloadingHandler;
            // TODO: locate/consider other delegate handlers for inclusion
        }

        private void DisableEventHandlers(IModApi modApi)
        {
            modApi.GameEvent -= GameEventHandler;
            modApi.Application.ChatMessageSent -= ChatMessageSentHandler;
            modApi.Application.GameEntered -= GameEnteredHandler;
            modApi.Application.OnPlayfieldLoaded -= OnPlayfieldLoadedHandler;
            modApi.Application.OnPlayfieldUnloading -= OnPlayfieldUnloadingHandler;
        }

        // *********************** event handlers ***********************

        async void GameEventHandler(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null)
        {
            // TODO: make GameEventType part of the topic (explicit subscribe)
            //if (type == GameEventType.HoldingItem) return;
            //if (type == GameEventType.PlayerStatChanged) return;
            //if (type == GameEventType.WindowClosed) return;
            //if (type == GameEventType.WindowOpened) return;
            //if (type == GameEventType.ArmorEquipped) return;
            //if (type == GameEventType.StatusEffectApplied) return;
            //if (type == GameEventType.InventoryOpened) return;
            //if (type == GameEventType.InventoryContains) return;
            //if (type == GameEventType.InventoryOpenedPoi) return;
            //if (type == GameEventType.ItemsConsumed) return;
            //if (type == GameEventType.InventoryClosedPoi) return;
            //if (type == GameEventType.BlockChanged) return;
            //if (type == GameEventType.ItemsPickedUp) return;
            //if (type == GameEventType.PlantHarvested) return;
            //if (type == GameEventType.InventoryContainsCountOfItem) return;
            //if (type == GameEventType.MainPowerSwitched) return;
            //if (type == GameEventType.WaitAction) return;
            //if (type == GameEventType.OpenedConstructor) return;
            //if (type == GameEventType.InventoryClosed) return;
            //if (type == GameEventType.ConstructionQueueContains) return;
            //if (type == GameEventType.ItemsCrafted) return;
            //if (type == GameEventType.StatusEffectRemoved) return;
            //if (type == GameEventType.DialogOption) return;
            //if (type == GameEventType.ToolbarContains) return;
            //if (type == GameEventType.BlockDestroyed) return;
            //if (type == GameEventType.ViewSelected) return;
            //if (type == GameEventType.DrilledOrFilledBlocks) return;

            try
            {
                JObject json = null;

                if (arg1 != null || arg2 != null || arg3 != null || arg4 != null || arg5 != null)
                {
                    json = new JObject();
                    if (arg1 != null) json.Add(new JProperty("Arg1", arg1.ToString()));
                    if (arg2 != null) json.Add(new JProperty("Arg2", arg2.ToString()));
                    if (arg3 != null) json.Add(new JProperty("Arg3", arg3.ToString()));
                    if (arg4 != null) json.Add(new JProperty("Arg4", arg4.ToString()));
                    if (arg5 != null) json.Add(new JProperty("Arg5", arg5.ToString()));
                }
                string jsonString = json?.ToString(Newtonsoft.Json.Formatting.None);
                await CTX.Messenger.SendAsync("ModApi.GameEvent." + type.ToString() + "/E", jsonString);
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

        async void ChatMessageSentHandler(MessageData chatMsgData)
        {
            // TODO: figure out when fired and how to encode results
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
            var playerEntityId = CTX.ModApi.Application.LocalPlayer.Id;
            var steamId = CTX.ModApi.Application.LocalPlayer.SteamId;
            var gameName = Path.GetFileName(CTX.ModApi.Application.GetPathFor(AppFolder.SaveGame));
            var cacheDir = CTX.ModApi.Application.GetPathFor(AppFolder.Cache);
            var directories = Directory.GetDirectories(cacheDir);
            var gameIdentifier = Path.GetFileName(directories.FirstOrDefault(dir => Path.GetFileName(dir).StartsWith(gameName)));
            JObject json = new JObject(
                    new JProperty("PlayerSteamId", steamId),
                    new JProperty("PlayerEntityId", playerEntityId),
                    new JProperty("GameName", gameName),
                    new JProperty("GameIdentifier", gameIdentifier),
                    new JProperty("HasEntered", hasEntered));
            await CTX.Messenger.SendAsync("ModApi.Application.GameEntered/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnPlayfieldLoadedHandler(IPlayfield playfield)
        {
            CTX.LoadedPlayfield.Add(new KeyValuePair<string, IPlayfield>(playfield.Name, playfield));   //CTX.LoadedPlayfield.Add(playfield.Name, playfield);
            playfield.OnEntityLoaded += OnEntityLoaded;
            playfield.OnEntityUnloaded += OnEntityUnloaded;
            JObject json = new JObject(
                    new JProperty("Name", playfield.Name),
                    new JProperty("PlayfieldType", playfield.PlayfieldType),
                    new JProperty("PlanetType", playfield.PlanetType),
                    new JProperty("PlanetClass", playfield.PlanetClass),
                    new JProperty("SolarSystemName", playfield.SolarSystemName),
                    new JProperty("SolarSystemCoordinates", playfield.SolarSystemCoordinates.ToString()),
                    new JProperty("IsPvP", playfield.IsPvP)
                    );
            await CTX.Messenger.SendAsync("ModApi.Application.OnPlayfieldLoaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnPlayfieldUnloadingHandler(IPlayfield playfield)
        {
            CTX.LoadedPlayfield.RemoveAll(x => x.Key == playfield.Name);    //CTX.LoadedPlayfield.Remove(playfield.Name);
            playfield.OnEntityLoaded -= OnEntityLoaded;
            playfield.OnEntityUnloaded -= OnEntityUnloaded;
            JObject json = new JObject(new JProperty("Name", playfield.Name)); // TODO: any other stuff to add to this?
            await CTX.Messenger.SendAsync("ModApi.Application.OnPlayfieldUnloading/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnEntityLoaded(IEntity entity)
        {
            CTX.LoadedEntity.Add(new KeyValuePair<int, IEntity>(entity.Id, entity));    //CTX.LoadedEntity.Add(entity.Id, entity); TODO: why did this syntax change?
            JObject json = new JObject(
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name),
                    new JProperty("Faction", entity.Faction.ToString()),
                    new JProperty("Position", entity.Position.ToString()),
                    new JProperty("Forward", entity.Forward.ToString()),
                    new JProperty("Rotation", entity.Rotation.ToString()),
                    new JProperty("IsLocal", entity.IsLocal),
                    new JProperty("IsProxy", entity.IsProxy),
                    new JProperty("IsPoi", entity.IsPoi),
                    new JProperty("BelongsTo", entity.BelongsTo),
                    new JProperty("DockedTo", entity.DockedTo),
                    new JProperty("Type", entity.Type),
                    new JProperty("Structure", null)    // TODO: figure out encoding/lookup for this
                    );
            await CTX.Messenger.SendAsync("ModApi.Playfield.EntityLoaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        async void OnEntityUnloaded(IEntity entity)
        {
            CTX.LoadedEntity.RemoveAll(x => x.Key == entity.Id);    //CTX.LoadedEntity.Remove(entity.Id);  TODO: confirm the entry > 1 case happens
            JObject json = new JObject(
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name)
                    );
            await CTX.Messenger.SendAsync("ModApi.Playfield.OnEntityUnloaded/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }

    }
}