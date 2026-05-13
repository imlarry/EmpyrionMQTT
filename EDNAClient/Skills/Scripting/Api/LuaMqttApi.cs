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
    ///   Each call to subscribe() registers one callback per topic filter; subscribing the same
    ///   filter twice replaces the earlier callback. For topics also consumed by C# skills,
    ///   prefer the broadcast model (EdnaService calls LuaScriptHost.Broadcast) rather than
    ///   subscribing directly from Lua.
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

        /// <summary>Publish a message. msgType must be one of: Req Res Evt Log</summary>
        public void publish(string scope, string msgTypeStr, string name, string payload)
        {
            EdnaLogger.Detail($"[{_engine.Name}] mqtt.publish {scope}/{msgTypeStr}/{name}");
            MessageType msgType;
            if (!System.Enum.TryParse(msgTypeStr, out msgType))
            {
                _ = _messenger.SendAsync("App", MessageType.Log, "LuaMqttApi.InvalidMsgType",
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
            /* stub -- raw topic filter subscriptions removed; reimplement via OnBroadcastRequest */
        }
    }
}
