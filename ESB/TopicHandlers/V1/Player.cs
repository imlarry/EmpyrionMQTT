using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Player : IPlayer
    {
        private readonly ContextData _ctx;

        public Player(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Player.GetInventory", GetInventory);
        }

        public async Task GetInventory(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                var entityId = Convert.ToInt32(applicationArgs.GetValue("EntityId"));
                var id = new Id(entityId);
                var inv = await _ctx.ModBase.Request_Player_GetInventory(id);
                JObject json = new JObject();
                if (inv != null)
                {
                    json.Add(new JProperty("Data", JObject.FromObject((Inventory)inv)));
                }
                else
                {
                    json.Add(new JProperty("Data", "<no data>"));
                }
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
