using System;
using System.Threading.Tasks;
using Eleon.Modding;
using EmpyrionNetAPIAccess;
using ESB.Models;
using ESB.EventHandlers;

// EmpyrionMQTT .. mod entrypoint for MQTT integration with Empyrion Galactic Survival

namespace ESB
{
    /// <summary>
    /// ESB mod entry point. Inherits both Empyrion API layers in a single class instance:
    ///
    ///   EmpyrionModBase (V1 / ModBase / DediAPI)
    ///     - Initialize() is called by the game ONLY on DedicatedServer in multiplayer.
    ///     - It is never invoked in SinglePlayer — V1 is a complete no-op in SP.
    ///     - Provides server-wide async RPC: inventory read/write, cross-PF teleport,
    ///       player connect/disconnect events, credits, faction graph, etc.
    ///
    ///   IMod (V2 / IModApi)
    ///     - Init() is called on every process that loads the mod:
    ///       Client, DedicatedServer, and PlayfieldServer.
    ///     - Provides object-oriented access to local process state (entities,
    ///       playfields, player cache). Scope of data varies by process type.
    ///
    /// Both APIs share a single ContextData instance (ModBase + ModApi fields).
    /// V1 handlers registered in SubscriptionHandler will only be reached on
    /// DedicatedServer in multiplayer; they are silently unreachable in SP or on Client.
    /// </summary>
    public class EmpyrionServiceBus : EmpyrionModBase, IMod, IEmpyrionServiceBus
    {
        // ********************************************
        // ************ Local Context Data ************
        // ********************************************

        private readonly ContextData _contextData = new ContextData();
        private IEventManager _eventManager;
        private IBusManager _busManager;
        private IGameManager _gameManager;

        public EmpyrionServiceBus() { } // no constructor as yet


        // ********************************************
        // ************ EmpyrionModBase API ***********
        // ********************************************
        public override void Initialize(ModGameAPI legacyModApi)
        {
            try
            {
                legacyModApi.Console_Write("ESB ModGameAPI start — ModBase initializing");
                _contextData.ModBase = this;
                legacyModApi.Console_Write($"ESB ModGameAPI start — ModBase assigned, Broker is {(Broker == null ? "NULL" : "set")}");
            }
            catch (Exception ex)
            {
                legacyModApi.Console_Write($"ESB Initialize failed: {ex}");
                for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                    legacyModApi.Console_Write($"  Caused by: {inner.GetType().Name}: {inner.Message}");
                throw;
            }
        }

        public new void Game_Start(ModGameAPI legacyModApi)
        {
            legacyModApi.Console_Write("ESB Game_Start called");
            base.Game_Start(legacyModApi);
        }

        public new void Game_Event(CmdId eventId, ushort seqNr, object data)
        {
            _contextData.ModApi?.Log($"ESB Game_Event: {eventId}");
            base.Game_Event(eventId, seqNr, data);
        }

        public new void Game_Exit()
        {
            _contextData.ModApi?.Log("ESB Game_Exit called");
            base.Game_Exit();
        }

        public new void Game_Update()
        {
            base.Game_Update();
        }


        // ********************************************
        // ***************** IMod API *****************
        // ********************************************
        public async void Init(IModApi modApi)
        {
            try
            {
                await InitInternalAsync(modApi);
            }
            catch (Exception ex)
            {
                modApi.Log($"ESB Init failed: {ex}");
                for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                    modApi.Log($"  Caused by: {inner.GetType().Name}: {inner.Message}");
                throw;
            }
        }

        private async Task InitInternalAsync(IModApi modApi)
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
            var updateHandler = factory.CreateUpdateHandler();

            // create the event manager
            _eventManager = new EventManager(_contextData, chatMessageSentHandler, entityLoadedHandler, entityUnloadedHandler, gameEnteredHandler, gameEventHandler, playfieldLoadedHandler, playfieldUnloadingHandler, updateHandler);

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
