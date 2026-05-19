using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ESB.Messaging
{
    public class MessageBus : IMessageBus
    {
        // SettleDelayMs ... after a context swap, pause briefly to give downstream subscribers
        // (e.g. EDNA reacting to a Broadcast GameEnter) time to complete their own swap before
        // the caller resumes publishing on the new rcId. Pragmatic cross-process band-aid; the
        // proper fix is the coordinated approach described in Docs/Bus/next-steps.md.
        private const int SettleDelayMs = 500;

        private readonly IMessenger      _messenger;
        private readonly string          _participantType;
        private readonly string          _host;
        private readonly int             _port;
        private readonly string          _username;
        private readonly string          _password;
        private readonly string          _caFilePath;
        private readonly HashSet<string> _audienceRcIds = new HashSet<string>();
        private string _contextRcId;
        private bool _connected;

        internal MessageBus(IMessenger messenger, string participantType,
            string host, int port, string username, string password, string caFilePath)
        {
            _messenger       = messenger;
            _participantType = participantType;
            _host            = host;
            _port            = port;
            _username        = username;
            _password        = password;
            _caFilePath      = caFilePath;
        }

        // -- Identity / diagnostics ----------------------------------------------

        public string ParticipantType  => _messenger.ParticipantType();
        public string MachineId        => _messenger.MachineId();
        public string ContextRcId      => _contextRcId;
        public string AvailableTopics() => _messenger.AvailableTopics();

        // -- Lifecycle -----------------------------------------------------------

        public async Task ConnectAsync()
        {
            await _messenger.ConnectAsync(_participantType,
                _host, _port, _username, _password, _caFilePath).ConfigureAwait(false);
            _connected = true;

            // Machine rcId and Broadcast rcId are subscribed by Messenger.ConnectAsync;
            // mirror them in the audience set for bookkeeping.
            _audienceRcIds.Add(_messenger.MachineId());
            _audienceRcIds.Add(RoutingContextId.BroadcastValue);
        }

        public Task DisconnectAsync() => _messenger.DisconnectAsync();

        // -- Audience subscriptions ---------------------------------------------

        public async Task SubscribeAsync(string routingContextId)
        {
            if (string.IsNullOrEmpty(routingContextId)) return;
            if (_audienceRcIds.Contains(routingContextId)) return;
            _audienceRcIds.Add(routingContextId);
            if (_connected)
                await _messenger.SubscribeBrokerAsync(routingContextId: routingContextId).ConfigureAwait(false);
        }

        public async Task UnsubscribeAsync(string routingContextId)
        {
            if (string.IsNullOrEmpty(routingContextId)) return;
            if (!_audienceRcIds.Contains(routingContextId)) return;
            _audienceRcIds.Remove(routingContextId);
            if (_connected)
                await _messenger.UnsubscribeAsync(routingContextId: routingContextId).ConfigureAwait(false);
        }

        // SwitchContextAsync ... sub new first, set ContextRcId, then unsub old. The new sub is
        // live before the old one drops so no in-process delivery gap exists across the swap.
        public async Task SwitchContextAsync(string newContextRcId)
        {
            if (string.IsNullOrEmpty(newContextRcId)) return;
            if (newContextRcId == _contextRcId) return;

            var previous = _contextRcId;

            if (!_audienceRcIds.Contains(newContextRcId))
            {
                _audienceRcIds.Add(newContextRcId);
                if (_connected)
                    await _messenger.SubscribeBrokerAsync(routingContextId: newContextRcId).ConfigureAwait(false);
            }

            _contextRcId = newContextRcId;

            if (!string.IsNullOrEmpty(previous) && _audienceRcIds.Contains(previous))
            {
                _audienceRcIds.Remove(previous);
                if (_connected)
                    await _messenger.UnsubscribeAsync(routingContextId: previous).ConfigureAwait(false);
            }

            // Settle: hold the caller briefly so downstream subscribers reacting to the same
            // context change (e.g. EDNA on GameEnter) can finish their own swap before we resume.
            await Task.Delay(SettleDelayMs).ConfigureAwait(false);
        }

        // -- Publish / announce --------------------------------------------------

        public Task PublishEventAsync<T>(string routingContextId, string scope, string operation, T payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            return _messenger.SendAsync(routingContextId,
                BusBuilder.NormalizeScope(scope), MessageType.Evt, operation, json);
        }

        public Task PublishContextEventAsync<T>(string scope, string operation, T payload)
        {
            return PublishEventAsync(_contextRcId, scope, operation, payload);
        }

        public Task AnnounceAsync<T>(string routingContextId, string operation, T payload, uint expirySeconds = 0u)
        {
            var json = JsonConvert.SerializeObject(payload);
            return _messenger.PublishRetainedAsync(routingContextId, "Announcements", MessageType.Evt, operation, json, expirySeconds);
        }

        public Task LogAsync(string routingContextId, string scope, string operation, string payload)
        {
            return _messenger.SendAsync(routingContextId, BusBuilder.NormalizeScope(scope), MessageType.Log, operation, payload);
        }

        // -- Request / response --------------------------------------------------

        public async Task<MessageEnvelope<TResponse>> RequestAsync<TRequest, TResponse>(
            string routingContextId, string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var raw = await SendRequest(routingContextId, scope, operation, payload, timeout).ConfigureAwait(false);
            return new MessageEnvelope<TResponse>(raw, BusBuilder.NormalizeScope(scope), operation);
        }

        public async Task<MessageEnvelope> RequestAsync<TRequest>(
            string routingContextId, string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var raw = await SendRequest(routingContextId, scope, operation, payload, timeout).ConfigureAwait(false);
            return new MessageEnvelope(raw, BusBuilder.NormalizeScope(scope), operation);
        }

        private async Task<string> SendRequest<TRequest>(
            string routingContextId, string scope, string operation, TRequest payload, TimeSpan timeout)
        {
            var json = JsonConvert.SerializeObject(payload);
            var raw  = await _messenger.RequestAsync(routingContextId,
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
        // Handlers are dispatch-key-keyed. The audience filter is the participant's
        // active SubscribeAsync rcIds; handlers read MessageEnvelope.RoutingContextId
        // if they need to act on the audience.

        public void OnEvent<T>(string scope, string operation,
            Func<MessageEnvelope<T>, Task> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope<T>.From(ctx)));
        }

        public void OnEvent(string scope, string operation,
            Func<MessageEnvelope, Task> handler)
        {
            var key = BusBuilder.NormalizeScope(scope) + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope.From(ctx)));
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
        }

        public void OnBroadcastRequest(string fromParticipantType, string scope, string operation,
            Func<MessageEnvelope, Task> handler)
        {
            var normScope = BusBuilder.NormalizeScope(scope);
            var key = normScope + "/evt/" + operation;
            _messenger.RegisterHandler(key, ctx => handler(MessageEnvelope.From(ctx)));
        }
    }
}
