using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public class Teleporter
    {
        private readonly ContextData _ctx;

        public Teleporter(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Teleporter.Get", Get);
            _ctx.Messenger.RegisterHandler("V2.Teleporter.Set", Set);
        }

        private async Task<Eleon.Modding.IStructure> GetStructure(string topic, int entityId)
        {
            var entity = _ctx.GetEntityByKey(entityId);
            if (entity == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                return null;
            }
            if (entity.Structure == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return entity.Structure;
        }

        private static JObject TargetDataJson(TeleporterData d) => new JObject(
            new JProperty("TargetEntityNameOrGroup", d.TargetEntityNameOrGroup),
            new JProperty("TargetPlayfield",         d.TargetPlayfield),
            new JProperty("TargetSolarSystemName",   d.TargetSolarSystemName),
            new JProperty("Origin",                  d.Origin));

        private static TeleporterData ParseTargetData(JToken t) => new TeleporterData(
            nameOrGroup:      t["TargetEntityNameOrGroup"]?.Value<string>(),
            playfield:        t["TargetPlayfield"]?.Value<string>(),
            solarSystemName:  t["TargetSolarSystemName"]?.Value<string>(),
            origin:           (byte)(t["Origin"]?.Value<int>() ?? byte.MaxValue));

        public async Task Get(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var teleporter = structure.GetDevice<Eleon.Modding.ITeleporter>(pos);
                    if (teleporter == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Teleporter device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    var data = teleporter.TargetData;
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)));
                    json.Merge(TargetDataJson(data));
                    await Task.CompletedTask;
                });

                if (json != null)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task Set(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                var data     = ParseTargetData(args);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var teleporter = structure.GetDevice<Eleon.Modding.ITeleporter>(pos);
                    if (teleporter == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No Teleporter device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    teleporter.TargetData = data;
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)));
                    json.Merge(TargetDataJson(data));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
