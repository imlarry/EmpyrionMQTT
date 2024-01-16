using Eleon.Modding;
using Eleon;
using EmpyrionNetAPIAccess;
using ESB.Common;
using ESB.EventHandlers;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using ESBLog.Database;
using System.Data.SQLite;

// EmpyrionMQTT .. mod entrypoint for MQTT integration with Empyrion Galactic Survival

namespace ESB
{
    public class EmpyrionServiceBus : EmpyrionModBase, IMod
   // public class EmpyrionServiceBus : IMod, ModInterface
    {
        // ********************************************
        // ************ Local Context Data ************ 
        // ********************************************

        private readonly ContextData _contextData = new ContextData();
        private EventManager _eventManager;
        private ESBManager _esbManager;

        public EmpyrionServiceBus()
        {
        }

        //ModGameAPI legacyModApi;

        //// ----- ModInterface methods -----------------------------------------

        //// Called once early when the host process starts - treat this like a constructor for your mod
        //public void Game_Start(ModGameAPI legacyModApi)
        //{
        //    this.legacyModApi = legacyModApi;
        //}

        //// Called once just before the game is shut down - treat this like a Dispose method to release unmanaged resources
        //public void Game_Exit()
        //{
        //}

        //public void Game_Event(CmdId eventId, ushort seqNr, object data)
        //{
        //    JObject json = new JObject(
        //        new JProperty("EventId", eventId.ToString())
        //        );
        //    Task.Run(async () =>
        //    {
        //        await _contextData.Messenger.SendAsync(MessageClass.Event, "LegacyGameEvent", json.ToString(Newtonsoft.Json.Formatting.None));
        //    });

        //}

        //// called each frame - don't waste time here!
        //public void Game_Update()
        //{
        //}

        // ********************************************
        // ************ EmpyrionModBase API ***********
        // ********************************************
        public override void Initialize(ModGameAPI dediAPI)
        {
            dediAPI.Console_Write("ESB ModGameAPI start");
            _contextData.ModBase = this;
            var factory = new EventHandlerFactory(_contextData);

            var legacyPlayfieldLoadedHandler = factory.CreateLegacyPlayfieldLoadedHandler();
            this.Event_Playfield_Loaded += legacyPlayfieldLoadedHandler.Handle;

            this.Event_Player_ChangedPlayfield += Handle_Event_Player_ChangedPlayfield;
        }

        private async void Handle_Event_Player_ChangedPlayfield(IdPlayfield obj)
        {
            var json = new JObject(
                new JProperty("PlayerEntityId", obj.id),
                new JProperty("PlayfieldName", obj.playfield)
                );
            await _contextData.Messenger.SendAsync(MessageClass.Event, "PlayerChangedPlayfield", json.ToString(Newtonsoft.Json.Formatting.None));
        }


        // ********************************************
        // ***************** IMod API *****************
        // ********************************************
        public async void Init(IModApi modApi)
        {
            // place game provided handle into context
            modApi.Log("ESB IMod API start");
            _contextData.ModApi = modApi;

            // create the event handlers
            var factory = new EventHandlerFactory(_contextData);
            var chatMessageSentHandler = factory.CreateChatMessageSentHandler();
            var entityLoadedHandler = factory.CreateEntityLoadedHandler();
            var entityUnloadedHandler = factory.CreateEntityUnloadedHandler();
            var gameEnteredHandler = factory.CreateGameEnteredHandler();
            var gameEventHandler = factory.CreateGameEventHandler();
            var playfieldLoadedHandler = factory.CreatePlayfieldLoadedHandler(entityLoadedHandler, entityUnloadedHandler);
            var playfieldUnloadingHandler = factory.CreatePlayfieldUnloadingHandler(entityLoadedHandler, entityUnloadedHandler);

            // create the event manager
            _eventManager = new EventManager(_contextData, chatMessageSentHandler, entityLoadedHandler, entityUnloadedHandler, gameEnteredHandler, gameEventHandler, playfieldLoadedHandler, playfieldUnloadingHandler);

            // initialize bus manager (message broker, database, etc)
            _esbManager = new ESBManager(_contextData, _eventManager);
            await _esbManager.Init();
        }

        public async void Shutdown()
        {
            _contextData.ModApi.Log("ESB IMod API exit");
            await _esbManager.Shutdown();
        }


    }
}