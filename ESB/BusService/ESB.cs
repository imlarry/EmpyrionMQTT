using Eleon.Modding;
using EmpyrionNetAPIAccess;
using ESB.Common;
using ESB.EventHandlers;

// EmpyrionMQTT .. mod entrypoint for MQTT integration with Empyrion Galactic Survival

namespace ESB
{
    public class EmpyrionServiceBus : EmpyrionModBase, IMod
    // public class EmpyrionServiceBus : IMod, ModInterface // debug event logging
    {
        // ********************************************
        // ************ Local Context Data ************ 
        // ********************************************

        private readonly ContextData _contextData = new ContextData();
        private EventManager _eventManager;
        private LegacyEventManager _legacyEventManager;
        private BusManager _busManager;
        private GameManager _gameManager;

        public EmpyrionServiceBus()
        {
        }

        // debug event logging
        //ModGameAPI legacyModApi;
        //public void Game_Start(ModGameAPI legacyModApi) {}
        //{
        //    this.legacyModApi = legacyModApi;
        //}
        //
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
        //
        //public void Game_Exit() {}
        //public void Game_Update() {}

        // ******************************************** TEST ME (requires changes to EmpyrionModBase)
        //public class EmpyrionModBase
        //{
        //    public virtual void Game_Event(CmdId eventId, ushort seqNr, object data)  // if you want to override the base class's implementation, use the "override" keyword .. potential for adding MQTT publish to ASTICs wrapper
        //    {
        //        // Existing implementation...
        //    }
        //}

        //public class EmpyrionServiceBus : EmpyrionModBase, IMod
        //{
        //    public override void Game_Event(CmdId eventId, ushort seqNr, object data)
        //    {
        //        // Your custom logic here...

        //        // Call the base class's implementation
        //        base.Game_Event(eventId, seqNr, data);
        //    }
        //}
        // ******************************************** TEST ME

        // ********************************************
        // ************ EmpyrionModBase API ***********
        // ********************************************
        public override void Initialize(ModGameAPI dediAPI)
        {
            dediAPI.Console_Write("ESB ModGameAPI start");
            _contextData.ModBase = this;
            var factory = new LegacyEventHandlerFactory(_contextData);
            var legacyPlayfieldLoadedHandler = factory.CreateLegacyPlayfieldLoadedHandler();
            _legacyEventManager = new LegacyEventManager(_contextData, legacyPlayfieldLoadedHandler);
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
            _busManager = new BusManager(_contextData, _eventManager);
            await _busManager.Init();

            // initialize game manager (game state and associated methods)
            _gameManager = new GameManager(_contextData);
            await _gameManager.Init();
        }

        public async void Shutdown()
        {
            _contextData.ModApi.Log("ESB IMod API exit");
            await _busManager.Shutdown();
        }

    }
}