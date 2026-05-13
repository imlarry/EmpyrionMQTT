using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public partial class GameEventHandler : HandlerBase, IGameEventHandler
    {
        public GameEventHandler(ContextData context) : base(context) { }

        public async void Handle(GameEventType type, object arg1 = null, object arg2 = null,
            object arg3 = null, object arg4 = null, object arg5 = null)
        {
            if (_suppressedEvents.Contains(type))
                return;

            ulong ticks;
            string mode;
            try { ticks = _ctx.ModApi.Application.GameTicks; mode = _ctx.ModApi.Application.Mode.ToString(); }
            catch { return; }

            try
            {
                var json = new JObject(
                    new JProperty("GameTicks", ticks),
                    new JProperty("Mode",      mode));

                object[] args = new object[] { arg1, arg2, arg3, arg4, arg5 };

                EventDef def;
                if (_eventDefs.TryGetValue(type, out def))
                {
                    for (int i = 0; i < def.Args.Length && i < args.Length; i++)
                    {
                        if (args[i] == null) continue;
                        var argDef = def.Args[i];
                        JToken value;
                        if (argDef.Transform != null)
                            value = argDef.Transform(args[i], _ctx);
                        else if (argDef.IsNumeric)
                        {
                            long lv;
                            double dv;
                            if (long.TryParse(args[i].ToString(), out lv))
                                value = new JValue(lv);
                            else if (double.TryParse(args[i].ToString(), out dv))
                                value = new JValue(dv);
                            else
                                value = new JValue(args[i].ToString());
                        }
                        else
                            value = new JValue(args[i].ToString());
                        json.Add(new JProperty(argDef.Name, value));
                    }
                }
                else
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] != null)
                            json.Add(new JProperty("Arg" + (i + 1), args[i].ToString()));
                    }
                }

                await _ctx.Bus.PublishEventAsync("App", type.ToString(), json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync("EventHandlers", "GameEvent", ex.ToString()); } catch { }
            }
        }
    }
}
