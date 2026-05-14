using System;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.Helpers;
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

                if (type == GameEventType.InventoryOpened)
                    TryAddContainerContents(json, arg2, arg4);

                var rcId = _ctx.GameManager.GameRcId ?? ESB.Messaging.RoutingContextId.BroadcastValue;
                await _ctx.Bus.PublishEventAsync(rcId, "App", type.ToString(), json);
            }
            catch (Exception ex)
            {
                try { await _ctx.Bus.LogAsync(_ctx.Bus.MachineId, "EventHandlers", "GameEvent", ex.ToString()); } catch { }
            }
        }

        // Append container Items to an InventoryOpened event. Best-effort; missing
        // arg shape, missing entity, or missing mapping leaves the base event untouched.
        // arg2 carries ContainerStructureNameId ("...,entityId=N,..."); arg4 carries Pos "(x,y,z)".
        private void TryAddContainerContents(JObject json, object arg2, object arg4)
        {
            try
            {
                if (arg2 == null || arg4 == null) return;
                var mapping = _ctx.GameManager.BlockAndItemMapping;
                if (mapping == null) return;

                int entityId;
                var s2parts = arg2.ToString().Split(',');
                if (s2parts.Length < 2) return;
                var kv = s2parts[1].Split('=');
                if (kv.Length < 2 || !int.TryParse(kv[1], out entityId)) return;

                var pparts = arg4.ToString().Trim('(', ')').Split(',');
                if (pparts.Length < 3) return;
                int px, py, pz;
                if (!int.TryParse(pparts[0].Trim(), out px)) return;
                if (!int.TryParse(pparts[1].Trim(), out py)) return;
                if (!int.TryParse(pparts[2].Trim(), out pz)) return;
                var pos = new VectorInt3(px, py, pz);

                var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
                if (pf == null) return;
                IEntity entity;
                if (!pf.Entities.TryGetValue(entityId, out entity) || entity == null || entity.Structure == null)
                    return;
                var container = entity.Structure.GetDevice<IContainer>(pos);
                if (container == null) return;
                var content = container.GetContent();
                if (content == null) return;

                json["Items"] = MessageHelpers.ItemStacksJson(content, mapping);
            }
            catch { /* enrichment best-effort */ }
        }
    }
}
