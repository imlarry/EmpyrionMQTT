using ESB.Models;
using ESB.Interfaces;
using static EmpyrionNetAPIAccess.EmpyrionModBase;

namespace ESB
{
    public class EventManager : IEventManager
    {
        private readonly ContextData _ctx;
        private readonly IChatMessageSentHandler _chatMessageSentHandler;
        private readonly IEntityLoadedHandler _entityLoadedHandler;
        private readonly IEntityUnloadedHandler _entityUnloadedHandler;
        private readonly IGameEnteredHandler _gameEnteredHandler;
        private readonly IGameEventHandler _gameEventHandler;
        private readonly IPlayfieldLoadedHandler _playfieldLoadedHandler;
        private readonly IPlayfieldUnloadingHandler _playfieldUnloadingHandler;
        private readonly IUpdateHandler _updateHandler;

        public EventManager
            (ContextData context
            , IChatMessageSentHandler chatMessageSentHandler
            , IEntityLoadedHandler entityLoadedHandler
            , IEntityUnloadedHandler entityUnloadedHandler
            , IGameEnteredHandler gameEnteredHandler
            , IGameEventHandler gameEventHandler
            , IPlayfieldLoadedHandler playfieldLoadedHandler
            , IPlayfieldUnloadingHandler playfieldUnloadingHandler
            , IUpdateHandler updateHandler
            )
        {
            _ctx = context;
            _chatMessageSentHandler = chatMessageSentHandler;
            _entityLoadedHandler = entityLoadedHandler;
            _entityUnloadedHandler = entityUnloadedHandler;
            _gameEnteredHandler = gameEnteredHandler;
            _gameEventHandler = gameEventHandler;
            _playfieldLoadedHandler = playfieldLoadedHandler;
            _playfieldUnloadingHandler = playfieldUnloadingHandler;
            _updateHandler = updateHandler;
        }

        public void EnableEventHandlers()
        {
            _ctx.ModApi.GameEvent += _gameEventHandler.Handle;
            _ctx.ModApi.Application.ChatMessageSent += _chatMessageSentHandler.Handle;
            _ctx.ModApi.Application.GameEntered += _gameEnteredHandler.Handle;
            _ctx.ModApi.Application.OnPlayfieldLoaded += _playfieldLoadedHandler.Handle;
            _ctx.ModApi.Application.OnPlayfieldUnloading += _playfieldUnloadingHandler.Handle;
            _ctx.ModApi.Application.Update += _updateHandler.Handle;
        }

        public void DisableEventHandlers()
        {
            _ctx.ModApi.GameEvent -= _gameEventHandler.Handle;
            _ctx.ModApi.Application.ChatMessageSent -= _chatMessageSentHandler.Handle;
            _ctx.ModApi.Application.GameEntered -= _gameEnteredHandler.Handle;
            _ctx.ModApi.Application.OnPlayfieldLoaded -= _playfieldLoadedHandler.Handle;
            _ctx.ModApi.Application.OnPlayfieldUnloading -= _playfieldUnloadingHandler.Handle;
            _ctx.ModApi.Application.Update -= _updateHandler.Handle;
        }

        /* testing template- test via mock events but not currently used...
        
        [Test]
        public void TestChatMessageSentHandler()
        {
            // Arrange
            var mockEvent = new Mock<IEvent>();
            var eventManager = new EventManager( PASS IN DEPENDENCIES );

            // Act
            eventManager.ChatMessageSentHandler.Handle(mockEvent.Object);

            // Assert
            // Verify that the handler behaved as expected
        }

        public IChatMessageSentHandler ChatMessageSentHandler { get; private set; }
        public IEntityLoadedHandler EntityLoadedHandler { get; private set; }
        public IEntityUnloadedHandler EntityUnloadedHandler { get; private set; }
        public IGameEnteredHandler GameEnteredHandler { get; private set; }
        public IGameEventHandler GameEventHandler { get; private set; }
        public IPlayfieldLoadedHandler PlayfieldLoadedHandler { get; private set; }
        public IPlayfieldUnloadingHandler PlayfieldUnloadingHandler { get; private set; }
        */
    }
}