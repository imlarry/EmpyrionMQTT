using ESB.Models;

namespace ESB.EventHandlers
{
    public class EventHandlerFactory : IEventHandlerFactory
    {
        private readonly ContextData _ctx;

        public EventHandlerFactory(ContextData context)
        {
            _ctx = context;
        }

        public ChatMessageSentHandler CreateChatMessageSentHandler()
        {
            return new ChatMessageSentHandler(_ctx);
        }

        public EntityLoadedHandler CreateEntityLoadedHandler()
        {
            return new EntityLoadedHandler(_ctx);
        }

        public EntityUnloadedHandler CreateEntityUnloadedHandler()
        {
            return new EntityUnloadedHandler(_ctx);
        }

        public GameEnteredHandler CreateGameEnteredHandler()
        {
            return new GameEnteredHandler(_ctx);
        }

        public GameEventHandler CreateGameEventHandler()
        {
            return new GameEventHandler(_ctx);
        }

        public PlayfieldLoadedHandler CreatePlayfieldLoadedHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler)
        {
            return new PlayfieldLoadedHandler(_ctx, entityLoadedHandler, entityUnloadedHandler);
        }

        public PlayfieldUnloadingHandler CreatePlayfieldUnloadingHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler)
        {
            return new PlayfieldUnloadingHandler(_ctx, entityLoadedHandler, entityUnloadedHandler);
        }
        public UpdateHandler CreateUpdateHandler()
        {
            return new UpdateHandler(_ctx);
        }

    }
}
