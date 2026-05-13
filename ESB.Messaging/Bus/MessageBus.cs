using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ESB.Messaging
{
    internal class SubscriptionSpec
    {
        public string      Scope     { get; }
        public MessageType MsgType   { get; }
        public string      Operation { get; }

        public SubscriptionSpec(string scope, MessageType msgType, string operation)
        {
            Scope     = scope;
            MsgType   = msgType;
            Operation = operation;
        }
    }

    public class MessageBus : IMessageBus
    {
        private readonly IMessenger             _messenger;
        private readonly string                 _participantType;
        private readonly string                 _host;
        private readonly int                    _port;
        private readonly string                 _username;
        private readonly string                 _password;
        private readonly string                 _caFilePath;
        private readonly List<SubscriptionSpec> _subscriptions;
        private bool _connected;
        private readonly HashSet<string> _subscribedKeys = new HashSet<string>();

        internal MessageBus(IMessenger messenger, string participantType,
            string host, int port, string username, string password, string caFilePath,
            List<SubscriptionSpec> subscriptions)
        {
            _messenger       = messenger;
            _participantType = participantType;
            _host            = host;
            _port            = port;
            _username        = username;
            _password        = password;
            _caFilePath      = caFilePath;
            _subscriptions   = subscriptions ?? new List<SubscriptionSpec>();
        }

        // -- Identity / diagnostics ----------------------------------------------

        public string ParticipantType  => _messenger.ParticipantType();
        public string ConnectionId     => _messenger.ClientId();
        public string AvailableTopics() => _messenger.AvailableTopics();

        // -- Lifecycle -----------------------------------------------------------

        public async Task ConnectAsync(BaseContextData ctx)
        {
            await _messenger.ConnectAsync(ctx, _participantType,
                _host, _port, _username, _password, _caFilePath).ConfigureAwait(false);
            _connected = true;

            // Auto-register the identity handler so other participants can discover this one.
            _messenger.RegisterHandler("Registry/req/Identify", async msgCtx => {
                if (msgCtx.ResponseTopic != null)
                {
                    var identJson = new JObject(
                        new JProperty("ParticipantType", ParticipantType),
                        new JProperty("ConnectionId",    ConnectionId));
                    await _messenger.ReplyAsync(msgCtx.ResponseTopic, msgCtx.CorrelationData,
                        identJson.ToString(Newtonsoft.Json.Formatting.None)).ConfigureAwait(false);
                }
            });
            await _messenger.SubscribeBrokerAsync(
                participantType: _participantType, connectionId: "discovery",
                scope: "Registry", msgType: MessageType.Req, operation: "Identify")
                .ConfigureAwait(false);

            foreach (var sub in _subscriptions)
            {
                await _messenger.SubscribeBrokerAsync(
                    scope: sub.Scope, msgType: sub.MsgType, operation: sub.Operation)
                    .ConfigureAwait(false);
            }
        }

        public Task DisconnectAsync() => _messenger.DisconnectAsync();

        // -- Publish / announce --------------------------------------------------

        public Task PublishEventAsync<T>(string scope, string operation, T payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            return _messenger.SendAsync(
                BusBuilder.NormalizeScope(scope), MessageType.Evt, operation, json);
        }

        public Task AnnounceAsync<T>(string operation, T payload, uint expirySeconds = 0u)
        {
            var json = JsonConvert.SerializeObject(payload);
            return _messenger.PublishRetainedAsync("Announcements", MessageType.Evt, operation, json, expirySeconds);
        }

        public Task LogAsync(string scope, string operation, string payload)
        {
            return _messenger.SendAsync(BusBuilder.NormalizeScope(scope), MessageType.Log, operation, payload);
        }

        // -- Request / response --------------------------------------------------

        public async Task<MessageEnvelope<TResponse>> RequestAsync<TRequest, TResponse>(
            string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var raw = await SendRequest(scope, operation, payload, timeout).ConfigureAwait(false);
            return new MessageEnvelope<TResponse>(raw, BusBuilder.NormalizeScope(scope), operation);
        }

        public async Task<MessageEnvelope> RequestAsync<TRequest>(
            string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var raw = await SendRequest(scope, operation, payload, timeout).ConfigureAwait(false);
            return new MessageEnvelope(raw, BusBuilder.NormalizeScope(scope), operation);
        }

        private async Task<string> SendRequest<TRequest>(
            string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var json = JsonConvert.SerializeObject(payload);
            var raw  = await _messenger.RequestAsync(
                BusBuilder.NormalizeScope(scope), operation, json, timeout).ConfigureAwait(false);
            CheckForBusError(raw);
            return raw;
        }

        private static void CheckForBusError(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            try
            {
                var obj = JObject.Parse(raw);
                var err = (string)obj["error"];
                if (!string.IsNullOrEmpty(err))
                    throw new BusRequestException(err);
            }
            catch (BusRequestException)
            {
                throw;
            }
            catch
            {
                // Non-JSON or no error field -- pass through.
            }
        }

        // -- Dynamic handler registration ----------------------------------------

        public void OnEvent<T>(string scope, string operation,
            Func<MessageEnvelope<T>, Task> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope<T>.From(ctx)));
            EnsureSubscribed(BusBuilder.NormalizeScope(scope), MessageType.Evt);
        }

        public void OnEvent(string scope, string operation,
            Func<MessageEnvelope, Task> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope.From(ctx)));
            EnsureSubscribed(BusBuilder.NormalizeScope(scope), MessageType.Evt);
        }

        public void OnRequest<TReq, TRes>(string scope, string operation,
            Func<MessageEnvelope<TReq>, Task<TRes>> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/req/" + operation;
            _messenger.RegisterHandler(key, async ctx => {
                var envelope = MessageEnvelope<TReq>.From(ctx);
                var response = await handler(envelope).ConfigureAwait(false);
                if (ctx.ResponseTopic != null)
                {
                    var json = JsonConvert.SerializeObject(response);
                    await _messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, json)
                                   .ConfigureAwait(false);
                }
            });
            EnsureSubscribed(BusBuilder.NormalizeScope(scope), MessageType.Req);
        }

        public void OnRequest(string scope, string operation,
            Func<MessageEnvelope, Task<string>> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/req/" + operation;
            _messenger.RegisterHandler(key, async ctx => {
                var envelope = MessageEnvelope.From(ctx);
                var json = await handler(envelope).ConfigureAwait(false);
                if (ctx.ResponseTopic != null && json != null)
                {
                    await _messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, json)
                                   .ConfigureAwait(false);
                }
            });
            EnsureSubscribed(BusBuilder.NormalizeScope(scope), MessageType.Req);
        }

        public void OnBroadcastRequest(string fromParticipantType, string scope, string operation,
            Func<MessageEnvelope, Task> handler)
        {
            var normScope = BusBuilder.NormalizeScope(scope);
            var key = normScope + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope.From(ctx)));
            EnsureSubscribed(normScope, MessageType.Evt);
        }

        public async Task<string> DiscoverConnectionIdAsync(string participantType, TimeSpan timeout)
        {
            var raw = await _messenger.RequestToAsync(
                participantType, "discovery", "Registry", "Identify", "{}", timeout)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(raw)) return null;
            var obj = JObject.Parse(raw);
            return (string)obj["ConnectionId"];
        }

        // One broker subscription per msgType covers all scopes; dispatch table routes by key.
        // Deduplicates so N OnRequest/OnEvent calls across all scopes produce one subscribe.
        private void EnsureSubscribed(string scope, MessageType msgType)
        {
            string key = msgType.ToString().ToLower();
            if (_subscribedKeys.Contains(key)) return;
            _subscribedKeys.Add(key);

            if (_connected)
                _messenger.SubscribeBrokerAsync(msgType: msgType);
            else
                _subscriptions.Add(new SubscriptionSpec(null, msgType, null));
        }
    }
}
