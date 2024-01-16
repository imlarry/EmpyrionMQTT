namespace ESB.EventHandlers
{
    public interface IEventHandlerFactory
    {
        ChatMessageSentHandler CreateChatMessageSentHandler();
        EntityLoadedHandler CreateEntityLoadedHandler();
        EntityUnloadedHandler CreateEntityUnloadedHandler();
        GameEnteredHandler CreateGameEnteredHandler();
        GameEventHandler CreateGameEventHandler();
        PlayfieldLoadedHandler CreatePlayfieldLoadedHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler);
        PlayfieldUnloadingHandler CreatePlayfieldUnloadingHandler(EntityLoadedHandler entityLoadedHandler, EntityUnloadedHandler entityUnloadedHandler);
    }
}