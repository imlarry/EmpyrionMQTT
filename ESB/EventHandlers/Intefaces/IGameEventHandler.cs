namespace ESB.Interfaces
{
    public interface IGameEventHandler
    {
        void Handle(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null);
    }
}