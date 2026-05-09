using System.Threading.Tasks;

namespace ESB.Messaging
{
    public interface IRequestHandler<TRequest, TResponse>
    {
        Task<TResponse> HandleAsync(MessageEnvelope<TRequest> envelope);
    }
}
