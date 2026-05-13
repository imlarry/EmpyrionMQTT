using System;
using System.Threading.Tasks;

namespace ESB.Messaging
{
    public interface IMessageBus
    {
        string ParticipantType { get; }
        string ConnectionId    { get; }
        string AvailableTopics();

        Task ConnectAsync(BaseContextData ctx);
        Task DisconnectAsync();

        Task PublishEventAsync<T>(string scope, string operation, T payload);

        // Publish a retained event to ESB/{ownType}/{ownConnId}/Announcements/evt/{operation}.
        Task AnnounceAsync<T>(string operation, T payload, uint expirySeconds = 0u);

        Task LogAsync(string scope, string operation, string payload);

        Task<MessageEnvelope<TResponse>> RequestAsync<TRequest, TResponse>(
            string scope, string operation, TRequest payload, TimeSpan timeout);

        Task<MessageEnvelope> RequestAsync<TRequest>(
            string scope, string operation, TRequest payload, TimeSpan timeout);

        void OnEvent<T>(string scope, string operation,
            Func<MessageEnvelope<T>, Task> handler);

        void OnEvent(string scope, string operation,
            Func<MessageEnvelope, Task> handler);

        void OnRequest<TReq, TRes>(string scope, string operation,
            Func<MessageEnvelope<TReq>, Task<TRes>> handler);

        void OnRequest(string scope, string operation,
            Func<MessageEnvelope, Task<string>> handler);

        // Subscribe to events or requests from another participant type using wildcard connectionId.
        // Subscribes to: ESB/{fromParticipantType}/+/{scope}/{msgType}/{operation}
        void OnBroadcastRequest(string fromParticipantType, string scope, string operation,
            Func<MessageEnvelope, Task> handler);

        // Discover the connectionId of a participant by sending a request to its "discovery" address.
        // Publishes to: ESB/{participantType}/discovery/Registry/req/Identify
        Task<string> DiscoverConnectionIdAsync(string participantType, TimeSpan timeout);
    }
}
