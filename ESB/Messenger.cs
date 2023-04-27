using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace ESBMessaging
{
    public abstract class BaseContextData
    {
        public Messenger Messenger { get; set; }
    }
    public class Messenger
    {
        private BaseContextData _ctx;
        private string _applicationId;
        private string _clientId;
        private MqttFactory _mqttFactory;
        private IMqttClient _mqttClient;
        private readonly Dictionary<string, Action<string, string>> _methods = new Dictionary<string, Action<string, string>>();

        public string RespondTo(string topic, string responseMsgclass)
        {
            string[] parts = topic.Split('/');
            return parts[2] + "/" + responseMsgclass;
        }

        // build client and connect to broker
        public async Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost")
        {
            _ctx = ctx;
            _applicationId = applicationId;
            _clientId = Guid.NewGuid().ToString().Substring(25);
            _mqttFactory = new MqttFactory();
            var mqttClientOptions = new MqttClientOptionsBuilder()
                    .WithTcpServer(withTcpServer)
                    .Build();
            _mqttClient = _mqttFactory.CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += ProcessMessageAsync;
            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            JObject json = new JObject(new JProperty("WithTcpServer", withTcpServer));
            await SendAsync("Messenger.ConnectAsync/I", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // disconnect from broker
        public async Task DisconnectAsync()
        {
            JObject json = new JObject(new JProperty("ClientId", _clientId));
            await SendAsync("Messenger.DisconnectAsync/I", json.ToString(Newtonsoft.Json.Formatting.None));
            await _mqttClient.DisconnectAsync();
        }

        // subscribe to a topic
        public async Task Subscribe(string topic, Action<string, string> topicHandler = null)
        {
            var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => { f.WithTopic(topic); })
                    .Build();
            if (topicHandler != null) 
            {
                string[] values = topic.Split('/');
                if (!_methods.ContainsKey(values[2]))
                {
                    _methods[values[2]] = topicHandler;
                }
            }
            await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
            JObject json = new JObject(new JProperty("Topic", topic));
            await SendAsync("Messenger.Subscribed/I", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // unsubscribe from a topic
        public async Task Unsubscribe(string topic)
        {
            await _mqttClient.UnsubscribeAsync(topic);
            JObject json = new JObject(new JProperty("Topic", topic));
            await SendAsync("Messenger.Unsubscribe/I", json.ToString(Newtonsoft.Json.Formatting.None));
        }

        // register a plugin DLL
        public async Task RegisterPlugin(string filename)
        {
            try
            {
                JObject json = new JObject(new JProperty("PluginFilename", filename));
                await SendAsync("Messenger/RegisterPlugin/I", json.ToString(Newtonsoft.Json.Formatting.None));
                var asm = Assembly.LoadFrom(filename);
                var pluginTypes = asm.GetTypes();
                foreach (var pluginType in pluginTypes)
                {
                    var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var method in methods)
                    {
                        if (method.ReturnType == typeof(void))
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string))
                            {
                                var instance = Activator.CreateInstance(pluginType, _ctx);
                                void action(string topic, string payload) => method.Invoke(instance, new object[] { topic, payload });
                                _methods.Add(pluginType.Namespace + "." + pluginType.Name + "." + method.Name, action);
                                await SendAsync("Messenger.RegisterPluginMethod/I", pluginType.Namespace + "." + pluginType.Name + "." + method.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await SendAsync("Messenger/RegisterPlugin/X", string.Format("Ex: {0}", ex.Message));
            }
            return;
        }

        // register local methods that are in the calling program
        public void RegisterLocalMethod(string methodName, Action<string, string> method)
        {
            _methods.Add(methodName, method);
        }

        // publish a message to the bus
        public async Task SendAsync(string topic, string payload)
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic(string.Format("{0}/{1}/{2}", "ESB", _applicationId, topic))  // TODO: include {3}, _clientId to instance id of publisher 
            .WithPayload(payload)
            .Build();
            await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
        }

        // return topic methods that have been registered as CSV TODO: jsonify
        public string AvailableTopics()
        {
            return string.Join(", ", _methods.Keys);
        }

        public string ClientId()
        { return _clientId; }

        // ProcessMessageAsync gets invoked in response to the broker delivering a publication on a subscribed topic
        private async Task ProcessMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = "";   // kludge to enforce string encoding with no nulls
            if (e.ApplicationMessage.PayloadSegment != null)
            {
                var payloadSegment = e.ApplicationMessage.PayloadSegment;
                var payloadArray = new byte[payloadSegment.Count];
                Array.Copy(payloadSegment.Array, payloadSegment.Offset, payloadArray, 0, payloadSegment.Count);
                payload = Encoding.Default.GetString(payloadArray);     // change byte array to string
            }

            // echo everything we get (debug output)
            JObject json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("Payload", payload)
                    );
            await SendAsync("Messenger.SubscribedMsgReceived/I", json.ToString(Newtonsoft.Json.Formatting.None));

            // invoke the "topic" method and passthru the message calling parms
            string[] values = topic.Split('/');
            if (_methods.TryGetValue(values[2], out var method))
            {
                method(topic, payload);
            }
            else
            {
                json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("Exception", "No handler " + values[2] + " defined"));
                await SendAsync("Messenger.ProcessMessageAsync/I", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}