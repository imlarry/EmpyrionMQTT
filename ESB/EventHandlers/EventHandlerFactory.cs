using ESB.Common;

namespace ESB.EventHandlers
{
    public class EventHandlerFactory
    {
        private readonly ContextData _contextData;

        public EventHandlerFactory(ContextData contextData)
        {
            _contextData = contextData;
        }

        public ChatMessageSentHandler CreateChatMessageSentHandler()
        {
            return new ChatMessageSentHandler(_contextData);
        }

        public EntityLoadedHandler CreateEntityLoadedHandler()
        {
            return new EntityLoadedHandler(_contextData);
        }

        public EntityUnloadedHandler CreateEntityUnloadedHandler()
        {
            return new EntityUnloadedHandler(_contextData);
        }

        public GameEnteredHandler CreateGameEnteredHandler()
        {
            return new GameEnteredHandler(_contextData);
        }

        public GameEventHandler CreateGameEventHandler()
        {
            return new GameEventHandler(_contextData);
        }

        public PlayfieldLoadedHandler CreatePlayfieldLoadedHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler)
        {
            return new PlayfieldLoadedHandler(_contextData, entityLoadedHandler, entityUnloadedHandler);
        }

        public PlayfieldUnloadingHandler CreatePlayfieldUnloadingHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler)
        {
            return new PlayfieldUnloadingHandler(_contextData, entityLoadedHandler, entityUnloadedHandler);
        }

    }
}
