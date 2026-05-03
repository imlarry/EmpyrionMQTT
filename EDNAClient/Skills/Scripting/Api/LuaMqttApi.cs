using EDNAClient.Core;
using ESB.Messaging;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace EDNAClient.Skills.Scripting.Api
{
    /// <summary>
    /// Exposes MQTT messaging to Lua scripts as the 'mqtt' global.
    ///
    /// Lua API:
    ///   mqtt.publish(scope, msgType, name, payload)   -- send an event to the bus (fire-and-forget)
    ///   mqtt.subscribe(topicFilter, fn)               -- direct broker subscription
    ///
    /// IMPORTANT -- subscribe() constraint:
    ///   The underlying Messenger routes messages by SubjectId (topic segment 3) and supports
    ///   only one handler per SubjectId. Subscribing from Lua to a SubjectId that is already
    ///   claimed by a C# skill (e.g. "Feeds.Scan") will replace that handler. Prefer the
    ///   broadcast model (EdnaService calls LuaScriptHost.Broadcast) for topics owned by C#.
    ///   Use subscribe() only for topics that are exclusively consumed by Lua scripts.
    /// </summary>
    [MoonSharpUserData]
    public sealed class LuaMqttApi
    {
        private readonly IMessenger _messenger;
        private readonly LuaEngine  _engine;

        internal LuaMqttApi(IMessenger messenger, LuaEngine engine)
        {
            _messenger = messenger;
            _engine    = engine;
        }

        /// <summary>Publish a message. msgType must be one of: Req Res Evt Err Log</summary>
        public void publish(string scope, string msgTypeStr, string name, string payload)
        {
            EdnaLogger.Detail($"[{_engine.Name}] mqtt.publish {scope}/{msgTypeStr}/{name}");
            MessageType msgType;
            if (!System.Enum.TryParse(msgTypeStr, out msgType))
            {
                _ = _messenger.SendAsync("App", MessageType.Err, "LuaMqttApi.InvalidMsgType",
                    $"{{\"Script\":\"{_engine.Name}\",\"MsgType\":{JsonConvert.SerializeObject(msgTypeStr)}}}");
                return;
            }
            _ = _messenger.SendAsync(scope, msgType, name, payload);
        }

        /// <summary>
        /// Subscribe to a topic filter. The callback fires on the WPF dispatcher thread,
        /// so it is safe to update ViewModels or call other WPF APIs from within it.
        ///
        /// Lua example:
        ///   mqtt.subscribe("ESB/Client/+/App/Evt/GameStarted", function(topic, payload)
        ///     log.info("got: " .. payload)
        ///   end)
        /// </summary>
        public void subscribe(string topicFilter, DynValue handler)
        {
            EdnaLogger.Detail($"[{_engine.Name}] mqtt.subscribe {topicFilter}");
            var task = _messenger.SubscribeEventAsync(topicFilter, (topic, payload) =>
            {
#if DEBUG
                _ = _messenger.SendAsync("App", MessageType.Log, "LuaMqttApi.Dispatch",
                    $"{{\"Script\":\"{_engine.Name}\",\"Topic\":\"{topic}\"}}");
#endif
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _engine.CallFunction(handler, topic, payload);
                    }
                    catch (ScriptRuntimeException ex)
                    {
                        _ = _messenger.SendAsync("App", MessageType.Log, "LuaMqttApi.CallbackError",
                            $"{{\"Script\":\"{_engine.Name}\",\"Topic\":\"{topic}\",\"Error\":{JsonConvert.SerializeObject(ex.DecoratedMessage)}}}");
                    }
                    catch (Exception ex)
                    {
                        _ = _messenger.SendAsync("App", MessageType.Log, "LuaMqttApi.CallbackError",
                            $"{{\"Script\":\"{_engine.Name}\",\"Topic\":\"{topic}\",\"Error\":{JsonConvert.SerializeObject(ex.GetType().Name + ": " + ex.Message)}}}");
                    }
                });
                return System.Threading.Tasks.Task.CompletedTask;
            });

            task.ContinueWith(t =>
            {
                var msg = t.Exception?.Flatten().InnerException?.Message ?? "unknown error";
                _ = _messenger.SendAsync("App", MessageType.Log, "LuaMqttApi.SubscribeFailed",
                    $"{{\"Script\":\"{_engine.Name}\",\"TopicFilter\":\"{topicFilter}\",\"Error\":{Newtonsoft.Json.JsonConvert.SerializeObject(msg)}}}");
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
