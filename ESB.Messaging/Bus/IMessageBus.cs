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

        Task AnnounceAsync<T>(string scope, string operation, T payload,
            uint expirySeconds = 0u);

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
    }

    // BusScope overloads -- keeps IMessageBus clean while providing enum convenience.
    public static class BusExtensions
    {
        public static Task PublishEventAsync<T>(
            this IMessageBus bus, BusScope scope, string operation, T payload)
            => bus.PublishEventAsync(scope.ToString(), operation, payload);

        public static Task AnnounceAsync<T>(
            this IMessageBus bus, BusScope scope, string operation, T payload,
            uint expirySeconds = 0u)
            => bus.AnnounceAsync(scope.ToString(), operation, payload, expirySeconds);

        public static Task<MessageEnvelope<TResponse>> RequestAsync<TRequest, TResponse>(
            this IMessageBus bus, BusScope scope, string operation,
            TRequest payload, TimeSpan timeout)
            => bus.RequestAsync<TRequest, TResponse>(scope.ToString(), operation, payload, timeout);

        public static Task<MessageEnvelope> RequestAsync<TRequest>(
            this IMessageBus bus, BusScope scope, string operation,
            TRequest payload, TimeSpan timeout)
            => bus.RequestAsync(scope.ToString(), operation, payload, timeout);

        public static void OnEvent<T>(
            this IMessageBus bus, BusScope scope, string operation,
            Func<MessageEnvelope<T>, Task> handler)
            => bus.OnEvent(scope.ToString(), operation, handler);

        public static void OnEvent(
            this IMessageBus bus, BusScope scope, string operation,
            Func<MessageEnvelope, Task> handler)
            => bus.OnEvent(scope.ToString(), operation, handler);

        public static void OnRequest<TReq, TRes>(
            this IMessageBus bus, BusScope scope, string operation,
            Func<MessageEnvelope<TReq>, Task<TRes>> handler)
            => bus.OnRequest(scope.ToString(), operation, handler);

        public static void OnRequest(
            this IMessageBus bus, BusScope scope, string operation,
            Func<MessageEnvelope, Task<string>> handler)
            => bus.OnRequest(scope.ToString(), operation, handler);
    }
}
