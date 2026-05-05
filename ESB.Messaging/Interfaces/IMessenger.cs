using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet.Client;

namespace ESB.Messaging
{
    public interface IMessenger
    {
        string MachineId();
        string ClientId();
        string ParticipantType();
        string AvailableTopics();
        MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", int port = 0, string username = null, string password = null, string caFilePath = null, string willTopic = null);
        Task ConnectAsync(BaseContextData ctx, string participantType, string withTcpServer = "localhost", int port = 1883, string username = null, string password = null, string caFilePath = null);
        Task DisconnectAsync();
        void RegisterHandler(string dispatchKey, Func<MessageContext, Task> handler);
        Task SubscribeBrokerAsync(string topicFilter);
        Task ReplyAsync(string responseTopic, byte[] correlationData, string payload);
        Task PublishRetainedAsync(string topic, string payload);
        Task PublishRetainedAsync(string topic, string payload, uint expirySeconds);
        Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback);
        Task UnsubscribeAsync(string topic);
        Task SendAsync(string scope, MessageType msgType, string operation, string payload);
        Task SendAsync(string scope, MessageType msgType, string operation, string payload, List<KeyValuePair<string, string>> userProperties);
        Task PublishAsync(string topic, string payload);  // raw publish for non-ESB schemas
        Task<string> RequestAsync(string scope, string operation, string payload, TimeSpan timeout);
    }
}
