using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet.Client;

namespace ESB.Messaging
{
    public interface IMessenger
    {
        int CompressionThreshold { get; set; }
        string MachineId();
        string ParticipantType();
        string AvailableTopics();
        MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", int port = 0, string username = null, string password = null, string caFilePath = null);
        Task ConnectAsync(string participantType, string withTcpServer = "localhost", int port = 1883, string username = null, string password = null, string caFilePath = null);
        Task DisconnectAsync();
        void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler);
        Task SubscribeBrokerAsync(string participantType = null, string routingContextId = null, string scope = null, MessageType? msgType = null, string operation = null);
        Task UnsubscribeAsync(string participantType = null, string routingContextId = null, string scope = null, MessageType? msgType = null, string operation = null);
        Task ReplyAsync(string responseTopic, byte[] correlationData, string payload);
        Task PublishRetainedAsync(string routingContextId, string scope, MessageType msgType, string operation, string payload, uint expirySeconds = 0u, bool compress = false);
        Task SendAsync(string routingContextId, string scope, MessageType msgType, string operation, string payload, List<KeyValuePair<string, string>> userProperties = null, bool compress = false);
        Task<string> RequestAsync(string routingContextId, string scope, string operation, string payload, TimeSpan timeout);
        // Send a request addressed to a specific participant type + routing context.
        Task<string> RequestToAsync(string targetParticipantType, string targetRoutingContextId, string scope, string operation, string payload, TimeSpan timeout);
    }
}
