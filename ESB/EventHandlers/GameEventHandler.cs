using ESB.Common;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;

namespace ESB
{
    public class GameEventHandler : IGameEventHandler
    {
        private readonly ContextData _cntxt;

        public GameEventHandler(ContextData cntxt)
        {
            _cntxt = cntxt;
        }
        public async void Handle(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null)
        {
            try
            {
                JObject json = new JObject(
                new JProperty("GameTicks", _cntxt.ModApi.Application.GameTicks),
                        new JProperty("Mode", _cntxt.ModApi.Application.Mode.ToString()));

                object[] args = new object[] { arg1, arg2, arg3, arg4, arg5 };

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null)
                    {
                        json.Add(new JProperty($"Arg{i + 1}", args[i].ToString()));
#if DEBUG
                        json.Add(new JProperty($"Arg{i + 1}Type", args[i].GetType().Name));
#endif
                    }
                }

                string jsonString = json?.ToString(Newtonsoft.Json.Formatting.None);
                await _cntxt.Messenger.SendAsync(MessageClass.Event, "GameEvent." + type.ToString(), jsonString);
            }
            catch (Exception ex)
            {
                JObject json = new JObject(
                        new JProperty("ErrorOnType", type.ToString()),
                new JProperty("Error", ex.Message)
                        );
                await _cntxt.Messenger.SendAsync(MessageClass.Exception, "GameEvent." + type.ToString(), json.ToString(Newtonsoft.Json.Formatting.None));
            }
        }
    }
}
