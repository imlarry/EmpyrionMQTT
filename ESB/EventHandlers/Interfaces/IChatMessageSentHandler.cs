using Eleon;

namespace ESB.Interfaces
{
    public interface IChatMessageSentHandler
    {
        void Handle(MessageData chatMsgData);
    }
}