using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json.Linq;


namespace ESB.Messaging
{
    public class Messenger : IMessenger
    {
        private BaseContextData _ctx;
        private string _machineId;
        private string _clientId;
        private string _participantType;
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;
        private readonly Dictionary<string, Func<MessageContext, Task>> _handlers = new Dictionary<string, Func<MessageContext, Task>>();
        private readonly Dictionary<string, Func<string, string, Task>> _eventCallbacks = new Dictionary<string, Func<string, string, Task>>();
        private readonly Dictionary<string, TaskCompletionSource<string>> _pendingResponses = new Dictionary<string, TaskCompletionSource<string>>();
        private readonly object _callbackLock = new object();

        // MachineId ... returns the MachineId associated with the connection
        public string MachineId()
        {
            return _machineId;
        }

        // ClientId ... returns the ClientId associated with the connection
        public string ClientId()
        {
            return _clientId;
        }

        // ParticipantType ... returns the participant type token used in ESB/ topics
        public string ParticipantType()
        {
            return _participantType;
        }

        // AvailableTopics ... returns registered dispatch keys as a CSV string
        public string AvailableTopics()
        {
            return string.Join(", ", _handlers.Keys);
        }

        // ParseTopic ... parses an ESB/ schema topic into a ParsedTopic.
        // Fixed 6-segment form: ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
        internal ParsedTopic ParseTopic(string topic)
        {
            var p  = topic.Split('/');
            var op = p[5];
            string metaOp = null;
            var dot = op.IndexOf('.');
            if (dot >= 0)
            {
                metaOp = op.Substring(dot + 1);
                op     = op.Substring(0, dot);
            }
            return new ParsedTopic
            {
                ParticipantType = p[1],
                ConnectionId    = p[2],
                Scope           = p[3],
                MsgType         = p[4],
                Operation       = op,
                MetaOperation   = metaOp,
                DispatchKey     = $"{p[3]}/{p[4]}/{op}"
            };
        }

        // MqttClientOptions ... function used to create an MQTT client options object
        public MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", int port = 0, string username = null, string password = null, string caFilePath = null, string willTopic = null)
        {
            int defaultPort = caFilePath != null ? 8883 : 1883;
            int resolvedPort = port > 0 ? port : defaultPort;
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(withTcpServer ?? "localhost", resolvedPort)
                .WithProtocolVersion(MqttProtocolVersion.V500);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                optionsBuilder.WithCredentials(username, password);
            }

            if (caFilePath != null)
            {
                X509Certificate2Collection certificates = new X509Certificate2Collection();
                certificates.Import(caFilePath);

                var tlsOptions = new MqttClientTlsOptionsBuilder()
                    .WithAllowUntrustedCertificates(true)   // fails in Unity sandbox
                    .WithIgnoreCertificateChainErrors(true)
                    .WithIgnoreCertificateRevocationErrors(true)
                    .WithClientCertificates(certificates)
                    .Build();
                optionsBuilder.WithTlsOptions(tlsOptions);
            }

            // Will message clears the registry entry if the client disconnects unexpectedly.
            if (!string.IsNullOrEmpty(willTopic))
            {
                optionsBuilder.WithWillTopic(willTopic)
                    .WithWillPayload("")
                    .WithWillRetain(true);
            }

            return optionsBuilder.Build();
        }

        // ConnectAsync ... build client and connect to broker
        public async Task ConnectAsync(BaseContextData ctx, string participantType, string withTcpServer = "localhost", int port = 1883, string username = null, string password = null, string caFilePath = null)
        {
            string tokenDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EmpyrionESB");
            Directory.CreateDirectory(tokenDir);
            string tokenPath = Path.Combine(tokenDir, "bus.token");
            string token;
            if (!File.Exists(tokenPath))
            {
                token = Guid.NewGuid().ToString();
                File.WriteAllText(tokenPath, token);
            }
            else
            {
                token = File.ReadAllText(tokenPath).Trim();
            }

            _ctx = ctx;
            _participantType = participantType;
            _machineId = IdentifierHelper.GenerateIdentifier(token, 6);
            _clientId  = IdentifierHelper.GenerateIdentifier(participantType + token, 4);
            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += ProcessMessageAsync;

            MqttClientOptions mqttClientOptions;
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password) && string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port);
            }
            else if (string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, username, password);
            }
            else
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, username, password, caFilePath);
            }

            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            // Subscribe once for all responses directed to this participant.
            await SubscribeBrokerAsync($"ESB/{_participantType}/{_clientId}/+/res/+");

            var now = DateTime.Now.ToString("s");
            var json = new JObject(
                new JProperty("WithTcpServer",   withTcpServer),
                new JProperty("ParticipantType", _participantType),
                new JProperty("MachineId",       _machineId),
                new JProperty("ClientId",        _clientId),
                new JProperty("ConnectedAt",     now));
            await LogAsync("ConnectAsync", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // DisconnectAsync ... disconnect from broker
        public async Task DisconnectAsync()
        {
            var json = new JObject(
                new JProperty("ClientId",       _clientId),
                new JProperty("DisconnectedAt", DateTime.Now.ToString("s")));
            await LogAsync("DisconnectAsync", json.ToString(Newtonsoft.Json.Formatting.None));
            await _mqttClient.DisconnectAsync();
        }

        // RegisterHandler ... register a handler by dispatch key
        public void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler)
        {
            if (handler != null)
                _handlers[dispatchKey] = handler;
        }

        // SubscribeBrokerAsync ... subscribe to an arbitrary broker topic filter
        public async Task SubscribeBrokerAsync(string topicFilter)
        {
            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topicFilter))
                .Build();
            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            var json = new JObject(
                new JProperty("TopicFilter",        topicFilter),
                new JProperty("RegisteredHandlers", AvailableTopics()));
            await LogAsync("Subscribed", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // ReplyAsync ... publish a response using MQTT5 ResponseTopic + CorrelationData
        public async Task ReplyAsync(string responseTopic, byte[] correlationData, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(responseTopic)
                .WithPayload(payload)
                .WithCorrelationData(correlationData)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // PublishRetainedAsync ... publish a retained message (used for registry entries)
        public async Task PublishRetainedAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // PublishRetainedAsync ... publish a retained message with MQTT5 MessageExpiryInterval
        public async Task PublishRetainedAsync(string topic, string payload, uint expirySeconds)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .WithMessageExpiryInterval(expirySeconds)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // SubscribeEventAsync ... subscribe to a topic filter with a raw (topic, payload) callback
        public async Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback)
        {
            lock (_callbackLock)
                _eventCallbacks[topicFilter] = callback;
            await SubscribeBrokerAsync(topicFilter);
        }

        // UnsubscribeAsync ... unsubscribe from a topic
        public async Task UnsubscribeAsync(string topic)
        {
            await _mqttClient.UnsubscribeAsync(topic);
            var json = new JObject(new JProperty("Topic", topic));
            await LogAsync("Unsubscribe", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // SendAsync ... publish to ESB/{participantType}/{connectionId}/{scope}/{msgType}/{operation}
        public async Task SendAsync(string scope, MessageType msgType, string operation, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"ESB/{_participantType}/{_clientId}/{scope}/{msgType.ToString().ToLower()}/{operation}")
                .WithPayload(payload)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // SendAsync ... same as above with MQTT5 user properties for point-to-point targeting
        public async Task SendAsync(string scope, MessageType msgType, string operation, string payload, List<KeyValuePair<string, string>> userProperties)
        {
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic($"ESB/{_participantType}/{_clientId}/{scope}/{msgType.ToString().ToLower()}/{operation}")
                .WithPayload(payload);
            foreach (var kv in userProperties)
                builder = builder.WithUserProperty(kv.Key, kv.Value);
            await _mqttClient.PublishAsync(builder.Build(), CancellationToken.None);
        }

        // PublishAsync ... raw publish to a fully-formed topic (for non-ESB schemas)
        public async Task PublishAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // RequestAsync ... publish a req with MQTT5 ResponseTopic; awaits and returns the reply payload
        public async Task<string> RequestAsync(string scope, string operation, string payload, TimeSpan timeout)
        {
            var shortId       = Guid.NewGuid().ToString("N").Substring(0, 8);
            var responseTopic = $"ESB/{_participantType}/{_clientId}/{scope}/res/{operation}";
            var tcs           = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_callbackLock)
                _pendingResponses[shortId] = tcs;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"ESB/{_participantType}/{_clientId}/{scope}/req/{operation}")
                .WithPayload(payload)
                .WithResponseTopic(responseTopic)
                .WithCorrelationData(Encoding.ASCII.GetBytes(shortId))
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);

            using (var cts = new CancellationTokenSource(timeout))
            {
                cts.Token.Register(() => tcs.TrySetCanceled());
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"RequestAsync {scope}/{operation}: no response within {(int)timeout.TotalSeconds}s");
                }
                finally
                {
                    lock (_callbackLock)
                        _pendingResponses.Remove(shortId);
                }
            }
        }

        // LogAsync ... emit an ESB/{participantType}/{connectionId}/App/log/{operation} message
        private Task LogAsync(string operation, string payload)
        {
            return SendAsync("App", MessageType.Log, operation, payload);
        }

        // ProcessMessageAsync ... invoked by MQTTnet on receipt of a subscribed message
        private async Task ProcessMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = null;

            if (e.ApplicationMessage.PayloadSegment.Count != 0)
            {
                var seg = e.ApplicationMessage.PayloadSegment;
                var buf = new byte[seg.Count];
                Array.Copy(seg.Array, seg.Offset, buf, 0, seg.Count);
                payload = Encoding.Default.GetString(buf);
            }
#if DEBUG
            var dbg = new JObject(new JProperty("Topic", topic), new JProperty("Payload", "[not included in this log]"));
            await LogAsync("ProcessingMessage", dbg.ToString(Newtonsoft.Json.Formatting.None));
#endif

            // Demux RequestAsync responses by correlation data before any other dispatch.
            var parts = topic.Split('/');
            if (parts.Length == 6 && parts[0] == "ESB" && parts[4] == "res")
            {
                var cd = e.ApplicationMessage.CorrelationData;
                if (cd != null && cd.Length > 0)
                {
                    var shortId = Encoding.ASCII.GetString(cd);
                    TaskCompletionSource<string> pendingTcs = null;
                    lock (_callbackLock)
                    {
                        if (_pendingResponses.TryGetValue(shortId, out pendingTcs))
                            _pendingResponses.Remove(shortId);
                    }
                    if (pendingTcs != null)
                    {
                        pendingTcs.TrySetResult(payload ?? "");
                        return;
                    }
                }
            }

            // Dispatch to raw event callbacks (direct subscriptions via SubscribeEventAsync)
            KeyValuePair<string, Func<string, string, Task>>[] callbacks;
            lock (_callbackLock)
            {
                callbacks = new KeyValuePair<string, Func<string, string, Task>>[_eventCallbacks.Count];
                int i = 0;
                foreach (var kv in _eventCallbacks) callbacks[i++] = kv;
            }
            foreach (var kv in callbacks)
            {
                if (MqttTopicFilterComparer.Compare(topic, kv.Key) == MqttTopicFilterCompareResult.IsMatch)
                    await kv.Value(topic, payload);
            }

            // ESB dispatch-key routing for well-formed ESB/{type}/{id}/{scope}/{msgType}/{op} topics
            if (parts.Length != 6 || parts[0] != "ESB") return;

            var pt = ParseTopic(topic);

            List<KeyValuePair<string, string>> userProps = null;
            if (e.ApplicationMessage.UserProperties != null && e.ApplicationMessage.UserProperties.Count > 0)
            {
                userProps = new List<KeyValuePair<string, string>>(e.ApplicationMessage.UserProperties.Count);
                foreach (var up in e.ApplicationMessage.UserProperties)
                    userProps.Add(new KeyValuePair<string, string>(up.Name, up.Value));
            }

            Func<MessageContext, Task> handler;
            _handlers.TryGetValue(pt.DispatchKey, out handler);

            if (handler != null)
            {
                await handler(new MessageContext
                {
                    ParsedTopic     = pt,
                    Payload         = payload,
                    ResponseTopic   = e.ApplicationMessage.ResponseTopic,
                    CorrelationData = e.ApplicationMessage.CorrelationData,
                    UserProperties  = userProps
                });
            }
            else
            {
                var json = new JObject(
                    new JProperty("Topic",     topic),
                    new JProperty("Exception", "No handler for " + pt.DispatchKey));
                await LogAsync("ProcessMessageAsync", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}
