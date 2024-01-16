using System;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace ESB.Messaging
{
    public interface IMessenger
    {
        string ApplicationId();
        string ClientId();
        string AvailableTopics();
        ParsedTopic ParseTopic(string topic);
        char MsgClass(MessageClass messageClass);
        MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", string username = null, string password = null, string caFilePath = null);
        Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost", string username = null, string password = null, string caFilePath = null);
        Task DisconnectAsync();
        Task SubscribeAsync(string topicOrSubjectId, Func<string, string, Task> topicHandler);
        Task UnsubscribeAsync(string topic);
        Task SendAsync(MessageClass messageClass, string topicOrSubjectId, string payload);
        Task SendAsync(string topic, string payload);
    }
}
