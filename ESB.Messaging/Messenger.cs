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
using System.Management;


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
        private readonly Dictionary<string, Func<string, string, Task>> _methods = new Dictionary<string, Func<string, string, Task>>();    // topic-to-action lookup

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
        public ParsedTopic ParseTopic(string topic)
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
        public MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", string username = null, string password = null, string caFilePath = null)
        {
            int? port = null;
            int defaultPort = caFilePath != null ? 8883 : 1883;
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(withTcpServer ?? "localhost", port ?? defaultPort)
                .WithProtocolVersion(MqttProtocolVersion.V500); // Use MQTT v5.0

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
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // ConnectAsync ... build client and connect to broker
        public async Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost", string username = null, string password = null, string caFilePath = null)
        {
            _ctx = ctx;
            _applicationId = applicationId;
            _machineId = GenerateMachineId(password);
            _clientId = Guid.NewGuid().ToString().Substring(25);
            _mqttFactory = new MqttFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += ProcessMessageAsync;

            MqttClientOptions mqttClientOptions;
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password) && string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer);
            }
            else if (string.IsNullOrEmpty(caFilePath))
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, username, password);
            }
            else
            {
                mqttClientOptions = CreateMqttClientOptions(withTcpServer, username, password, caFilePath);
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

        // Subscribe ... subscribe to a topic and register topic handler
        public async Task SubscribeAsync(string topicOrSubjectId, Func<string, string, Task> topicHandler)
        {
            string topic;
            string multicastTopic;
            if (topicOrSubjectId.Contains("/"))
            {
                topic = topicOrSubjectId;
                var topicParts = topicOrSubjectId.Split('/');
                topicParts[3] = "*";
                multicastTopic = string.Join("/", topicParts);
            }
            else
            {
                topic = $"{_applicationId}/Q/{topicOrSubjectId}/{_clientId}/+";
                multicastTopic = $"{_applicationId}/Q/{topicOrSubjectId}/*/+";
            }

            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => { f.WithTopic(topic); })
                    .WithTopicFilter(f => { f.WithTopic(multicastTopic); })
                    .Build();
            if (topicHandler != null)
            {
                var pt = ParseTopic(topic);
                if (!_methods.ContainsKey(pt.SubjectId))
                {
                    _methods[pt.SubjectId] = topicHandler;
                }
            }
            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            JObject json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("MulticastTopic", multicastTopic));
            await SendAsync(MessageClass.Information, "Messenger.Subscribed", json.ToString(Newtonsoft.Json.Formatting.None));
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
