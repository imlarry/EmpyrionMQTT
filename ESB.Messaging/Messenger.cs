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
        private int _pubSeqId = 0;
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;
        private readonly Dictionary<string, Func<string, string, Task>> _methods = new Dictionary<string, Func<string, string, Task>>();
        private readonly Dictionary<string, Func<EmpMessageContext, Task>> _empMethods = new Dictionary<string, Func<EmpMessageContext, Task>>();

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

        // ClientId ... returns the ClintId associated with the connection
        public string ClientId()
        {
            return _clientId;
        }

        // AvailableTopics ... returns topic methods that have been registered as a CSV string
        public string AvailableTopics()
        {
            return string.Join(", ", _methods.Keys);
        }

        // ParseTopic .. used to parse a topic string and return a ParsedTopic object
        internal ParsedTopic ParseTopic(string topic)
        {
            var parts = topic.Split('/');
            var parsedTopic = new ParsedTopic
            {
                SourceId = parts[0],
                MessageClass = parts[1],
                SubjectId = parts[2],
                ClientId = parts[3],
                PubSeqId = parts[4]
            };
            return parsedTopic;
        }

        // ParseEmpTopic ... parses an EMP/ schema topic into an EmpParsedTopic.
        // Standard (6 segments):      EMP/{type}/{connId}/{scope}/{dir}/{op}
        // Device sub-scope (8 segs):  EMP/{type}/{connId}/Structure/Device/{deviceName}/{dir}/{op}
        internal EmpParsedTopic ParseEmpTopic(string topic)
        {
            var p = topic.Split('/');
            // device sub-scope: parts[3]=="Structure" && parts[4]=="Device"
            if (p.Length >= 8 && p[3] == "Structure" && p[4] == "Device")
            {
                var devDir = p[6];
                var devOp  = string.Join("/", p, 7, p.Length - 7);
                return new EmpParsedTopic
                {
                    ParticipantType = p[1],
                    ConnectionId    = p[2],
                    Scope           = p[3],
                    DeviceName      = p[5],
                    Dir             = devDir,
                    Operation       = devOp,
                    DispatchKey     = $"Structure/Device/{p[5]}/{devDir}/{devOp}"
                };
            }
            // standard form: EMP/{type}/{connId}/{scope}/{dir}/{op...}
            // operation may be multi-segment: "get/GameTicks", "call/Teleport"
            var stdDir = p[4];
            var stdOp  = string.Join("/", p, 5, p.Length - 5);
            return new EmpParsedTopic
            {
                ParticipantType = p[1],
                ConnectionId    = p[2],
                Scope           = p[3],
                DeviceName      = null,
                Dir             = stdDir,
                Operation       = stdOp,
                DispatchKey     = $"{p[3]}/{stdDir}/{stdOp}"
            };
        }

        // MsgClass ... function used to convert MessageClass enum into topic encoding
        public char MsgClass(MessageClass messageClass)
        {
            switch (messageClass)
            {
                case MessageClass.Request: return 'Q';
                case MessageClass.Response: return 'R';
                case MessageClass.Event: return 'E';
                case MessageClass.Information: return 'I';
                case MessageClass.Exception: return 'X';
                default:
                    throw new ArgumentException(message: "Invalid enum value", paramName: nameof(messageClass));
            }
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

            string willTopic = $"EMP/Registry/{_clientId}";
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
            JObject json = new JObject(
                    new JProperty("WithTcpServer", withTcpServer),
                    new JProperty("Application", _applicationId),
                    new JProperty("MachineId", _machineId),
                    new JProperty("ClientId", _clientId),
                    new JProperty("ConnectedAt", now)
                    );
            await _ctx.Messenger.SendAsync(MessageClass.Information, "Messenger.ConnectAsync", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // DisconnectAsync ... disconnect from broker
        public async Task DisconnectAsync()
        {
            var now = DateTime.Now.ToString("s");
            JObject json = new JObject(
                    new JProperty("ClientId", _ctx.Messenger.ClientId()),
                    new JProperty("DisconnectedAt", now)
                    );
            await SendAsync(MessageClass.Information, "Messenger.DisconnectAsync", json.ToString(Newtonsoft.Json.Formatting.None));
            await _mqttClient.DisconnectAsync();
        }

        // RegisterHandler ... register a local dispatch handler by subject ID (no broker round-trip)
        public void RegisterHandler(string subjectId, Func<string, string, Task> handler)
        {
            if (handler != null)
                _methods[subjectId] = handler;
        }

        // RegisterEmpHandler ... register an emp/ schema handler by dispatch key (e.g. "player/req/get")
        public void RegisterEmpHandler(string dispatchKey, Func<EmpMessageContext, Task> handler)
        {
            if (handler != null)
                _empMethods[dispatchKey] = handler;
        }

        // SubscribeRequestsAsync ... single broker subscription covering all registered handlers
        // Matches: {appId}/Q/{anyHandler}/{myClientId}/{seqNum}  (targeted)
        //      and {appId}/Q/{anyHandler}/*/{seqNum}             (multicast)
        public async Task SubscribeRequestsAsync()
        {
            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"{_applicationId}/Q/+/{_clientId}/#"))
                .WithTopicFilter(f => f.WithTopic($"{_applicationId}/Q/+/*/#"))
                .Build();
            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            var json = new JObject(
                new JProperty("TargetedFilter",  $"{_applicationId}/Q/+/{_clientId}/#"),
                new JProperty("MulticastFilter", $"{_applicationId}/Q/+/*/#"),
                new JProperty("RegisteredHandlers", AvailableTopics()));
            await SendAsync(MessageClass.Information, "Messenger.Subscribed", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // SubscribeBrokerAsync ... subscribe to an arbitrary broker topic filter with no dispatch wiring.
        // Use this for schema-specific filters constructed by the caller (e.g. SubscriptionHandler).
        public async Task SubscribeBrokerAsync(string topicFilter)
        {
            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topicFilter))
                .Build();
            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
        }

        // ReplyAsync ... publish a response to an emp/ request using MQTT5 ResponseTopic + CorrelationData
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

        // SubscribeEventAsync ... subscribe directly to a broker topic filter (for event consumers
        // such as EDNA). Registers the callback by SubjectId for local dispatch, then subscribes
        // to the broker. Use this for E/I class topics, not for Q (request) handlers.
        public async Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback)
        {
            var pt = ParseTopic(topicFilter);
            if (callback != null)
                _methods[pt.SubjectId] = callback;
            var options = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topicFilter))
                .Build();
            await _mqttClient.SubscribeAsync(options, CancellationToken.None);
        }

        // Unsubscribe ... unsubscribe from a topic
        public async Task UnsubscribeAsync(string topic)
        {
            await _mqttClient.UnsubscribeAsync(topic);
            JObject json = new JObject(new JProperty("Topic", topic));
            await SendAsync(MessageClass.Information, "Messenger.Unsubscribe", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // SendAsync ... publish a message to the bus (three flavors)
        public async Task SendAsync(MessageClass messageClass, string topicOrSubjectId, string payload)    // form topic from the parts or alter the message class
        {
            string topic;
            if (topicOrSubjectId.Contains("/"))
            {
                var topicParts = topicOrSubjectId.Split('/');
                topicParts[1] = MsgClass(messageClass).ToString();
                topic = string.Join("/", topicParts);
            }
            else
            {
                topic = $"{_applicationId}/{MsgClass(messageClass)}/{topicOrSubjectId}/{_clientId}/{Interlocked.Increment(ref _pubSeqId)}";
            }

            var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .Build();
            await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
        }

        public async Task SendAsync(string topic, string payload)                                   // send a message and assume the topic is fully formed
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .Build();
            await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

            var pt = ParseTopic(topic);
            if (pt.SourceId == "Self")
            {
                await Task.Run(() => ProcessMessageAsync(topic, payload));
            }
        }

        private async Task ProcessMessageAsync(string topic, string payload)
        {
            JObject json;

#if DEBUG 
            // echo everything we get (debug output) 
            json = new JObject( new JProperty("Topic", topic), new JProperty("Payload", payload) ); 
            await SendAsync(MessageClass.Information, "Messenger.ProcessMessageAsync", json.ToString(Newtonsoft.Json.Formatting.None)); 
#endif
            var pt = ParseTopic(topic);

            if (_methods.TryGetValue(pt.SubjectId, out var method))
            {
                await method(topic, payload);
            }
            else
            {
                json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("Exception", "No handler " + pt.SubjectId + " defined"));
                await SendAsync(MessageClass.Information, "Messenger.ProcessMessageAsync", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        // ProcessMessageAsync ... invoked in response to the broker delivering a publication on a subscribed topic
        private async Task ProcessMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = null;
            JObject json;

            // if there is a payload then encode it as a string
            if (e.ApplicationMessage.PayloadSegment.Count != 0)
            {
                var payloadSegment = e.ApplicationMessage.PayloadSegment;
                var payloadArray = new byte[payloadSegment.Count];
                Array.Copy(payloadSegment.Array, payloadSegment.Offset, payloadArray, 0, payloadSegment.Count);
                payload = Encoding.Default.GetString(payloadArray);
            }
#if DEBUG 
            // echo everything we get (debug output) 
            json = new JObject( new JProperty("Topic", topic), new JProperty("Payload", payload) ); 
            await SendAsync(MessageClass.Information, "Messenger.ProcessingMessage", json.ToString(Newtonsoft.Json.Formatting.None)); 
#endif
            if (topic.StartsWith("EMP/"))
            {
                var ept = ParseEmpTopic(topic);
                string responseTopic = e.ApplicationMessage.ResponseTopic;
                byte[] correlationData = e.ApplicationMessage.CorrelationData;
                if (_empMethods.TryGetValue(ept.DispatchKey, out var empMethod))
                {
                    await empMethod(new EmpMessageContext
                    {
                        ParsedTopic     = ept,
                        Payload         = payload,
                        ResponseTopic   = responseTopic,
                        CorrelationData = correlationData
                    });
                }
                else
                {
                    json = new JObject(
                        new JProperty("Topic", topic),
                        new JProperty("Exception", "No emp handler " + ept.DispatchKey + " defined"));
                    await SendAsync(MessageClass.Information, "Messenger.ProcessMessageAsync", json.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
            else
            {
                var pt = ParseTopic(topic);

                if (_methods.TryGetValue(pt.SubjectId, out var method))
                {
                    await method(topic, payload);
                }
                else
                {
                    json = new JObject(
                        new JProperty("Topic", topic),
                        new JProperty("Exception", "No handler " + pt.SubjectId + " defined"));
                    await SendAsync(MessageClass.Information, "Messenger.ProcessMessageAsync", json.ToString(Newtonsoft.Json.Formatting.None));
                }
            }
        }
    }
}
