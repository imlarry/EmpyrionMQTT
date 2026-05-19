using System;
using System.Threading.Tasks;

namespace ESB.Messaging
{
    public interface IMessageBus
    {
        string ParticipantType { get; }
        string MachineId       { get; }
        // Current audience for this participant's events. Null until SwitchContextAsync is called.
        // Pfs/Ds set this once at startup (real Game rcId); Client/EDNA seed it to Lobby and swap to
        // Game on game-entry, back to Lobby on exit.
        string ContextRcId     { get; }
        string AvailableTopics();

        Task ConnectAsync();
        Task DisconnectAsync();

        Task PublishEventAsync<T>(string routingContextId, string scope, string operation, T payload);

        // Publishes an event to the participant's current ContextRcId. Equivalent to
        // PublishEventAsync(ContextRcId, scope, operation, payload), but reads ContextRcId at send
        // time so callers don't have to track swaps.
        Task PublishContextEventAsync<T>(string scope, string operation, T payload);

        // Publishes a retained event to ESB/{ownType}/{routingContextId}/Announcements/evt/{operation}.
        Task AnnounceAsync<T>(string routingContextId, string operation, T payload, uint expirySeconds = 0u);

        Task LogAsync(string routingContextId, string scope, string operation, string payload);

        Task<MessageEnvelope<TResponse>> RequestAsync<TRequest, TResponse>(
            string routingContextId, string scope, string operation, TRequest payload, TimeSpan timeout);

        Task<MessageEnvelope> RequestAsync<TRequest>(
            string routingContextId, string scope, string operation, TRequest payload, TimeSpan timeout);

        // Audience subscriptions. Add a routing context to receive messages addressed to it;
        // remove to stop. Machine rcId and Broadcast rcId are auto-subscribed on Connect.
        Task SubscribeAsync(string routingContextId);
        Task UnsubscribeAsync(string routingContextId);

        // Atomically swap the participant's context rcId. Subscribes the new rcId first so the
        // new audience is live before the old one is dropped, eliminating the in-process gap where
        // events on either rcId could be missed. Sets ContextRcId to newContextRcId.
        Task SwitchContextAsync(string newContextRcId);

        void OnEvent<T>(string scope, string operation,
            Func<MessageEnvelope<T>, Task> handler);

        void OnEvent(string scope, string operation,
            Func<MessageEnvelope, Task> handler);

        void OnRequest<TReq, TRes>(string scope, string operation,
            Func<MessageEnvelope<TReq>, Task<TRes>> handler);

        void OnRequest(string scope, string operation,
            Func<MessageEnvelope, Task<string>> handler);

        // Subscribe to events or requests from another participant type (handler-side registration).
        // Audience scope is governed by the participant's active SubscribeAsync rcIds.
        void OnBroadcastRequest(string fromParticipantType, string scope, string operation,
            Func<MessageEnvelope, Task> handler);
    }
}
