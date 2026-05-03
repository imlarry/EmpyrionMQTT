using EDNAClient.Core;
using ESB.Messaging;
using MoonSharp.Interpreter;
using Newtonsoft.Json;

namespace EDNAClient.Skills.Scripting.Api
{
    /// <summary>
    /// Exposes structured logging to Lua scripts as the 'log' global.
    ///
    /// Lua API:
    ///   log.info("message")   — debug trace only
    ///   log.warn("message")   — debug trace + EDNA/I/LuaLog.Warn on MQTT
    ///   log.error("message")  — debug trace + EDNA/I/LuaLog.Error on MQTT
    ///
    /// warn/error are always published to MQTT (not #if DEBUG) so script errors
    /// are visible in the bus without a debugger attached.
    /// </summary>
    [MoonSharpUserData]
    public sealed class LuaLogApi
    {
        private readonly string    _scriptName;
        private readonly IMessenger? _messenger;

        internal LuaLogApi(string scriptName, IMessenger? messenger = null)
        {
            _scriptName = scriptName;
            _messenger  = messenger;
        }

        public void info(string message)
            => EdnaLogger.Log($"[Lua:{_scriptName}] {message}");

        public void warn(string message)
        {
            EdnaLogger.Warn($"[Lua:{_scriptName}] {message}");
            Publish("LuaLog.Warn", message);
        }

        public void error(string message)
        {
            EdnaLogger.Error($"[Lua:{_scriptName}] {message}");
            Publish("LuaLog.Error", message);
        }

        private void Publish(string subjectId, string message)
        {
            if (_messenger == null) return;
            _ = _messenger.SendAsync("App", MessageType.Log, subjectId,
                $"{{\"Script\":{JsonConvert.SerializeObject(_scriptName)},\"Message\":{JsonConvert.SerializeObject(message)}}}");
        }
    }
}
