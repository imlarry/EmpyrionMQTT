using Eleon;

namespace ESB.Intefaces
{
    public interface IChatMessageSentHandler
    {
        void Handle(MessageData chatMsgData);
    }
}