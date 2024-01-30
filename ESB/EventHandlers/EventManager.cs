using ESB.Common;
using ESB.Interfaces;
using static EmpyrionNetAPIAccess.EmpyrionModBase;

namespace ESB
{
    public class EventManager
    {
        readonly private ContextData _cntxt;
        readonly private IChatMessageSentHandler _chatMessageSentHandler;
        readonly private IEntityLoadedHandler _entityLoadedHandler;
        readonly private IEntityUnloadedHandler _entityUnloadedHandler;
        readonly private IGameEnteredHandler _gameEnteredHandler;
        readonly private IGameEventHandler _gameEventHandler;
        readonly private IPlayfieldLoadedHandler _playfieldLoadedHandler;
        readonly private IPlayfieldUnloadingHandler _playfieldUnloadingHandler;
        readonly private IUpdateHandler _updateHandler;

        public EventManager
            (ContextData cntxt
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
            _cntxt = cntxt;
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
            _cntxt.ModApi.GameEvent += _gameEventHandler.Handle;
            _cntxt.ModApi.Application.ChatMessageSent += _chatMessageSentHandler.Handle;
            _cntxt.ModApi.Application.GameEntered += _gameEnteredHandler.Handle;
            _cntxt.ModApi.Application.OnPlayfieldLoaded += _playfieldLoadedHandler.Handle;
            _cntxt.ModApi.Application.OnPlayfieldUnloading += _playfieldUnloadingHandler.Handle;
            _cntxt.ModApi.Application.Update += _updateHandler.Handle;
        }

        public void DisableEventHandlers()
        {
            _cntxt.ModApi.GameEvent -= _gameEventHandler.Handle;
            _cntxt.ModApi.Application.ChatMessageSent -= _chatMessageSentHandler.Handle;
            _cntxt.ModApi.Application.GameEntered -= _gameEnteredHandler.Handle;
            _cntxt.ModApi.Application.OnPlayfieldLoaded -= _playfieldLoadedHandler.Handle;
            _cntxt.ModApi.Application.OnPlayfieldUnloading -= _playfieldUnloadingHandler.Handle;
            _cntxt.ModApi.Application.Update -= _updateHandler.Handle;
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