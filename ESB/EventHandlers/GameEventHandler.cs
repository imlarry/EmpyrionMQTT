using Eleon.Modding;
using ESB.Helpers;
using ESB.Interfaces;
using Newtonsoft.Json.Linq;

namespace ESB.EventHandlers
{
    public partial class GameEventHandler : HandlerBase, IGameEventHandler
    {
        public GameEventHandler(ContextData context) : base(context) { }

        public async void Handle(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null)
        {
            await Execute(async () =>
            {
                if (_suppressedEvents.Contains(type)) return;

                JObject json = new JObject(
                new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks));

                object[] args = new object[] { arg1, arg2, arg3, arg4, arg5 };

                EventDef def;
                _eventDefs.TryGetValue(type, out def);

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null) continue;

                    string fieldName;
                    JToken fieldVal;

                    if (def != null && i < def.Args.Length)
                    {
                        ArgDef ad = def.Args[i];
                        fieldName = ad.Name;
                        if (ad.Transform != null)
                        {
                            fieldVal = ad.Transform(args[i], _ctx);
                        }
                        else if (ad.IsNumeric)
                        {
                            long n;
                            if (long.TryParse(args[i].ToString(), out n))
                                fieldVal = new JValue(n);
                            else
                                fieldVal = new JValue(args[i].ToString());
                        }
                        else
                        {
                            fieldVal = new JValue(args[i].ToString());
                        }
                    }
                    else
                    {
                        fieldName = $"Arg{i + 1}";
                        fieldVal  = new JValue(args[i].ToString());
                    }

                    json.Add(new JProperty(fieldName, fieldVal));
                }

                if (type == GameEventType.InventoryOpened)
                {
                    var entityId = int.Parse(arg2.ToString().Split(',')[1].Split('=')[1]);

                    string[] parts = arg4.ToString().Trim('(', ')').Split(',');
                    VectorInt3 vector = new VectorInt3(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    var pf = _ctx.ModApi.ClientPlayfield;
                    IEntity ent;
                    if (pf != null && pf.Entities.TryGetValue(entityId, out ent) && ent != null && ent.Structure != null)
                    {
                        var content = ent.Structure.GetDevice<IContainer>(vector).GetContent();
                        var contentsJson = MessageHelpers.ItemStacksJson(content, _ctx.GameManager.BlockAndItemMapping);
                        json.Add(new JProperty("ItemStack", contentsJson));
                    }
                }

                string jsonString = json?.ToString(Newtonsoft.Json.Formatting.None);
                await EmitEventAsync("App", type.ToString(), jsonString);
            });
        }
    }
}
