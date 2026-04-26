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
        char MsgClass(MessageClass messageClass);
        MqttClientOptions CreateMqttClientOptions(string withTcpServer = "localhost", int port = 0, string username = null, string password = null, string caFilePath = null, string willTopic = null);
        Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost", int port = 1883, string username = null, string password = null, string caFilePath = null);
        Task DisconnectAsync();
        void RegisterHandler(string subjectId, Func<string, string, Task> handler);
        void RegisterEmpHandler(string dispatchKey, Func<EmpMessageContext, Task> handler);
        Task SubscribeRequestsAsync();
        Task SubscribeBrokerAsync(string topicFilter);
        Task ReplyAsync(string responseTopic, byte[] correlationData, string payload);
        Task PublishRetainedAsync(string topic, string payload);
        Task SubscribeEventAsync(string topicFilter, Func<string, string, Task> callback);
        Task UnsubscribeAsync(string topic);
        Task SendAsync(MessageClass messageClass, string topicOrSubjectId, string payload);
        Task SendAsync(string topic, string payload);
    }
}
