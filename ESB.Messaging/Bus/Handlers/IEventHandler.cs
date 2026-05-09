using System.Threading.Tasks;

namespace ESB.Messaging
{
    public interface IEventHandler<TEvent>
    {
        Task HandleAsync(MessageEnvelope<TEvent> envelope);
    }
}
