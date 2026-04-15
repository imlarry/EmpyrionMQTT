using Eleon.Modding;
using ESB.Interfaces;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;

namespace ESB
{
    public class GameEventHandler : HandlerBase, IGameEventHandler
    {
        public GameEventHandler(ContextData context) : base(context) { }

        public async void Handle(GameEventType type, object arg1 = null, object arg2 = null, object arg3 = null, object arg4 = null, object arg5 = null)
        {
            await Execute(async () =>
            {
                JObject json = new JObject(
                new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                new JProperty("Mode", _ctx.ModApi.Application.Mode.ToString()));

                object[] args = new object[] { arg1, arg2, arg3, arg4, arg5 };

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null)
                    {
                        json.Add(new JProperty($"Arg{i + 1}", args[i].ToString()));
#if DEBUG
                        //json.Add(new JProperty($"Arg{i + 1}Type", args[i].GetType().Name));
#endif
                    }
                }

                if (type == GameEventType.InventoryOpened)
                {
                    var entityId = int.Parse(arg2.ToString().Split(',')[1].Split('=')[1]);

                    string[] parts = arg4.ToString().Trim('(', ')').Split(',');
                    VectorInt3 vector = new VectorInt3(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    var content = _ctx.LoadedEntity[entityId].Structure.GetDevice<IContainer>(vector).GetContent();
                    var contentsJson = MessageHelpers.ItemStacksJson(content, _ctx.GameManager.BlockAndItemMapping);
                    json.Add(new JProperty("ItemStack", contentsJson));
                }
                if (type == GameEventType.InventoryOpenedPoi || type == GameEventType.InventoryClosedPoi)
                {
                    // TODO: confirm that these are always associated with the InventoryOpened event
                    // use OpenedContainer to append Container info to json
                    // set OpenedContainer to null
                }

                string jsonString = json?.ToString(Newtonsoft.Json.Formatting.None);
                await _ctx.Messenger.SendAsync(MessageClass.Event, "GameEvent." + type.ToString(), jsonString);
            });
        }
    }
}
