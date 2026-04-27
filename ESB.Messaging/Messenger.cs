using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
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
        private string _applicationId;
        private string _machineId;
        private string _clientId;
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;
        private readonly Dictionary<string, Func<MessageContext, Task>> _handlers = new Dictionary<string, Func<MessageContext, Task>>();

        // ApplicationId ... returns the ApplicationId associated with the connection
        public string ApplicationId()
        {
            return _applicationId;
        }

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

        // AvailableTopics ... returns registered dispatch keys as a CSV string
        public string AvailableTopics()
        {
            return string.Join(", ", _handlers.Keys);
        }

        // ParseTopic ... parses an EMP/ schema topic into a ParsedTopic.
        // Fixed 6-segment form: EMP/{participantType}/{connectionId}/{dir}/{scope}/{operation}
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
                Dir             = p[3],
                Scope           = p[4],
                Operation       = op,
                MetaOperation   = metaOp,
                DispatchKey     = $"{p[4]}/{op}"
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

        // GetMacAddress ... function used to get the MAC address of the first active network interface
        public static string GetMacAddress()
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    return nic.GetPhysicalAddress().ToString();
                }
            }
            return String.Empty;
        }

        // GenerateMachineId ... function used to generate a machine id from MAC address and a secret key
        public static string GenerateMachineId(string secretKey)
        {
            string macAddress = GetMacAddress();
            string rawId = macAddress + (secretKey ?? "");

            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                return IdEncoder.ToBase36(bytes, 6);
            }
        }

        // ConnectAsync ... build client and connect to broker
        public async Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost", int port = 1883, string username = null, string password = null, string caFilePath = null)
        {
            _ctx = ctx;
            _applicationId = applicationId;
            _machineId = GenerateMachineId(password);
            _clientId = IdEncoder.ToBase36(Guid.NewGuid().ToByteArray(), 4);
            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += ProcessMessageAsync;

            string willTopic = $"ESB/Registry/{_clientId}";
            MqttClientOptions mqttClientOptions;
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password) && string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, willTopic: willTopic);
            }
            else if (string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, username, password, willTopic: willTopic);
            }
            else
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, port, username, password, caFilePath, willTopic);
            }

            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            var now = DateTime.Now.ToString("s");
            var json = new JObject(
                new JProperty("WithTcpServer", withTcpServer),
                new JProperty("Application",   _applicationId),
                new JProperty("MachineId",     _machineId),
                new JProperty("ClientId",      _clientId),
                new JProperty("ConnectedAt",   now));
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

        // SubscribeEventAsync ... subscribe to an ESB/ event topic filter; stub pending EDNA rework
        public Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback)
        {
            throw new NotImplementedException("SubscribeEventAsync: pending EDNA rework for ESB/ schema");
        }

        // UnsubscribeAsync ... unsubscribe from a topic
        public async Task UnsubscribeAsync(string topic)
        {
            await _mqttClient.UnsubscribeAsync(topic);
            var json = new JObject(new JProperty("Topic", topic));
            await LogAsync("Unsubscribe", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // SendAsync ... publish a message to a fully-formed topic
        public async Task SendAsync(string topic, string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        // LogAsync ... emit an ESB/Messenger/{connId}/App/Log/{operation} message
        private Task LogAsync(string operation, string payload)
        {
            return SendAsync($"ESB/Messenger/{_clientId}/App/Log/{operation}", payload);
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
            var dbg = new JObject(new JProperty("Topic", topic), new JProperty("Payload", payload));
            await LogAsync("ProcessingMessage", dbg.ToString(Newtonsoft.Json.Formatting.None));
#endif
            var pt = ParseTopic(topic);

            Func<MessageContext, Task> handler;
            _handlers.TryGetValue(pt.DispatchKey, out handler);

            if (handler != null)
            {
                await handler(new MessageContext
                {
                    ParsedTopic     = pt,
                    Payload         = payload,
                    ResponseTopic   = e.ApplicationMessage.ResponseTopic,
                    CorrelationData = e.ApplicationMessage.CorrelationData
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
