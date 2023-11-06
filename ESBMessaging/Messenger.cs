using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            if (parts[0] == "ESB")
            {
                return parts[2] + "/" + responseMsgclass;   // TODO: replace with better topic review
            }
            return topic + "/" + responseMsgclass;
        }

        // build client and connect to broker
        public async Task ConnectAsync(BaseContextData ctx, string applicationId, string withTcpServer = "localhost")
        // TODO: 
        // use websocket connection (small messages / low latency)
        // use SecuteString stored credentials (no plaintext passwords)
        // use persisted ClientId value on a per game basis to partition data for services when dealing with multiple games
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

            var now = DateTime.Now.ToString("s");
            JObject json = new JObject(
                    new JProperty("WithTcpServer", withTcpServer),
                    new JProperty("Application", _applicationId),
                    new JProperty("ClientId", _clientId),
                    new JProperty("ConnectedAt", now)
                    );
            await _ctx.Messenger.SendAsync("Messenger.ConnectAsync/I", json.ToString(Newtonsoft.Json.Formatting.None));

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
                if (!_methods.ContainsKey(values[3]))
                {
                    _methods[values[3]] = topicHandler;
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
            Dictionary<string, Action<string, string>> tempMethods = new Dictionary<string, Action<string, string>>();
            List<string> methodList = new List<string>();
            try
            {
                var asm = Assembly.LoadFrom(filename);
                var pluginTypes = asm.GetTypes();
                foreach (var pluginType in pluginTypes)
                {
                    var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var method in methods)
                    {
                        if (method.ReturnType != typeof(void)) continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(string)) continue;

                        var instance = Activator.CreateInstance(pluginType, _ctx);
                        var extendedName = pluginType.Namespace + "." + pluginType.Name + "." + method.Name;
                        void action(string topic, string payload) => method.Invoke(instance, new object[] { topic, payload });
                        tempMethods.Add(extendedName, action);
                        methodList.Add(extendedName);
                    }
                }

                foreach (var item in tempMethods)
                {
                    _methods.Add(item.Key, item.Value);
                }

                JObject json = new JObject(
                    new JProperty("PluginFilename", filename),
                    new JProperty("MethodList", new JArray(methodList))
                );
                await SendAsync("Messenger/RegisterPlugin/I", json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception loaderEx in ex.LoaderExceptions)
                {
                    sb.AppendLine(loaderEx.Message);
                }
                await SendAsync("Messenger/RegisterPlugin.ReflectionTypeLoadException/X", string.Format("Ex: {0}", sb.ToString()));
            }
            catch (Exception ex)
            {
                await SendAsync("Messenger/RegisterPlugin/X", string.Format("Ex: {0}", ex.Message));
            }
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
                    .WithTopic(string.Format("{0}/{1}/{2}/{3}", "ESB", _applicationId, _clientId, topic)) 
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
            string topic = e.ApplicationMessage.Topic;
            string payload = null;

            // if there is a payload then encode it as a string (TODO: any reason to handle binary data?)
            if (e.ApplicationMessage.PayloadSegment.Count != 0)
            {
                var payloadSegment = e.ApplicationMessage.PayloadSegment;
                var payloadArray = new byte[payloadSegment.Count];
                Array.Copy(payloadSegment.Array, payloadSegment.Offset, payloadArray, 0, payloadSegment.Count);
                payload = Encoding.Default.GetString(payloadArray);
            }

            // echo everything we get (debug output)
            JObject json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("Payload", payload)
                    );
            await SendAsync("Messenger.SubscribedMsgReceived/I", json.ToString(Newtonsoft.Json.Formatting.None));

            string[] values = topic.Split('/');
            if (_methods.TryGetValue(values[3], out var method))
            {
                method(topic, payload);
            }
            else
            {
                json = new JObject(
                    new JProperty("Topic", topic),
                    new JProperty("Exception", "No handler " + values[3] + " defined"));
                await SendAsync("Messenger.ProcessMessageAsync/I", json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}