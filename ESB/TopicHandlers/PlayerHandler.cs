using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public partial class PlayerHandler
    {
        private readonly ContextData _ctx;

        public PlayerHandler(ContextData ctx) { _ctx = ctx; }

        // -------------------------------------------------------------------------
        // Property getter table -- allocated once at class load, keyed by name.
        // -------------------------------------------------------------------------

        static JToken Vec3(Vector3 v)    => new JObject(new JProperty("X", v.x), new JProperty("Y", v.y), new JProperty("Z", v.z));
        static JToken Vec4(Quaternion q) => new JObject(new JProperty("X", q.x), new JProperty("Y", q.y), new JProperty("Z", q.z), new JProperty("W", q.w));
        static JToken FD(FactionData fd) => new JObject(new JProperty("Group", fd.Group.ToString()), new JProperty("Id", fd.Id));

        static readonly Dictionary<string, Func<IPlayer, JToken>> _getters =
            new Dictionary<string, Func<IPlayer, JToken>>
        {
            ["Id"]                       = p => JToken.FromObject(p.Id),
            ["Name"]                     = p => JToken.FromObject(p.Name),
            ["SteamId"]                  = p => JToken.FromObject(p.SteamId),
            ["SteamOwnerId"]             = p => JToken.FromObject(p.SteamOwnerId),
            ["StartPlayfield"]           = p => JToken.FromObject(p.StartPlayfield),
            ["Origin"]                   = p => JToken.FromObject(p.Origin),
            ["Permission"]               = p => JToken.FromObject(p.Permission),
            ["Health"]                   = p => JToken.FromObject(p.Health),
            ["HealthMax"]                = p => JToken.FromObject(p.HealthMax),
            ["Oxygen"]                   = p => JToken.FromObject(p.Oxygen),
            ["OxygenMax"]                = p => JToken.FromObject(p.OxygenMax),
            ["Stamina"]                  = p => JToken.FromObject(p.Stamina),
            ["StaminaMax"]               = p => JToken.FromObject(p.StaminaMax),
            ["Food"]                     = p => JToken.FromObject(p.Food),
            ["FoodMax"]                  = p => JToken.FromObject(p.FoodMax),
            ["Radiation"]                = p => JToken.FromObject(p.Radiation),
            ["RadiationMax"]             = p => JToken.FromObject(p.RadiationMax),
            ["BodyTemp"]                 = p => JToken.FromObject(p.BodyTemp),
            ["BodyTempMax"]              = p => JToken.FromObject(p.BodyTempMax),
            ["Credits"]                  = p => JToken.FromObject(p.Credits),
            ["ExperiencePoints"]         = p => JToken.FromObject(p.ExperiencePoints),
            ["UpgradePoints"]            = p => JToken.FromObject(p.UpgradePoints),
            ["Kills"]                    = p => JToken.FromObject(p.Kills),
            ["Died"]                     = p => JToken.FromObject(p.Died),
            ["Ping"]                     = p => JToken.FromObject(p.Ping),
            ["HomeBaseId"]               = p => JToken.FromObject(p.HomeBaseId),
            ["IsPilot"]                  = p => JToken.FromObject(p.IsPilot),
            ["IsLocal"]                  = p => JToken.FromObject(p.IsLocal),
            ["IsProxy"]                  = p => JToken.FromObject(p.IsProxy),
            ["IsPoi"]                    = p => JToken.FromObject(p.IsPoi),
            ["BelongsTo"]                = p => JToken.FromObject(p.BelongsTo),
            ["DockedTo"]                 = p => JToken.FromObject(p.DockedTo),
            ["Type"]                     = p => JToken.FromObject(p.Type.ToString()),
            ["FactionData"]              = p => FD(p.FactionData),
            ["Faction"]                  = p => FD(p.Faction),
            ["FactionRole"]              = p => JToken.FromObject(p.FactionRole.ToString()),
            ["CurrentStructureId"]       = p => p.CurrentStructure != null ? JToken.FromObject(p.CurrentStructure.Id)        : JValue.CreateNull(),
            ["CurrentStructureEntityId"] = p => p.CurrentStructure != null ? JToken.FromObject(p.CurrentStructure.Entity.Id) : JValue.CreateNull(),
            ["DrivingEntityId"]          = p => p.DrivingEntity    != null ? JToken.FromObject(p.DrivingEntity.Id)            : JValue.CreateNull(),
            ["Position"]                 = p => Vec3(p.Position),
            ["Forward"]                  = p => Vec3(p.Forward),
            ["Rotation"]                 = p => Vec4(p.Rotation),
            ["Toolbar"]                  = p => SerializeItemStacks(p.Toolbar),
            ["Bag"]                      = p => SerializeItemStacks(p.Bag),
        };

        static JToken SerializeItemStacks(List<ItemStack> stacks)
        {
            var arr = new JArray();
            if (stacks == null) return arr;
            foreach (var s in stacks)
                arr.Add(new JObject(
                    new JProperty("Id",      s.id),
                    new JProperty("Count",   s.count),
                    new JProperty("SlotIdx", s.slotIdx),
                    new JProperty("Ammo",    s.ammo),
                    new JProperty("Decay",   s.decay)));
            return arr;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Player/GetProperties", Properties);
            _ctx.Messenger.RegisterHandler("Player/Teleport",      Teleport);
            _ctx.Messenger.RegisterHandler("Player/DamageEntity",  DamageEntity);
            _ctx.Messenger.RegisterHandler("Player/Describe",      Describe);
        }

        public async Task Describe(MessageContext ctx)
        {
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx,
                HandlerHelper.ScopeManifestJson("Player", _opDefs));
        }

        public async Task Properties(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetProperties", _opDefs["GetProperties"]);
                return;
            }
            try
            {
                JObject args = string.IsNullOrWhiteSpace(ctx.Payload) ? null : JObject.Parse(ctx.Payload);

                HashSet<string> filter = null;
                var propArray = args?["Properties"] as JArray;
                if (propArray != null)
                {
                    if (!HandlerHelper.TryParsePropertyNames(propArray, _getters.Keys, out filter, out var invalid))
                    {
                        var errObj = new JObject(
                            new JProperty("Error",             "InvalidProperty"),
                            new JProperty("InvalidProperties", new JArray(invalid)),
                            new JProperty("ValidProperties",   new JArray(_getters.Keys)));
                        await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, errObj.ToString(Formatting.None));
                        return;
                    }
                }

                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode"));
                    return;
                }

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    json = HandlerHelper.BuildPropertyObject(localPlayer, _getters, filter);
                    await Task.CompletedTask;
                });

                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task Teleport(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "Teleport", _opDefs["Teleport"]);
                return;
            }
            try
            {
                var lp = _ctx.ModApi.Application.LocalPlayer;
                if (lp == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode"));
                    return;
                }

                var args = JObject.Parse(ctx.Payload);
                string playfield = args["Playfield"]?.ToString();

                if (args["Pos"] == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("Pos argument is required"));
                    return;
                }
                if (playfield != null && args["Rot"] == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("Rot argument is required when Playfield is provided"));
                    return;
                }

                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    bool result = playfield == null
                        ? lp.Teleport(pos)
                        : lp.Teleport(playfield, pos, MessageHelpers.ParseVec3(args["Rot"]));
                    await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx,
                        new JObject(new JProperty("ok", result)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task DamageEntity(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "DamageEntity", _opDefs["DamageEntity"]);
                return;
            }
            try
            {
                var lp = _ctx.ModApi.Application.LocalPlayer;
                if (lp == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode"));
                    return;
                }

                var args = JObject.Parse(ctx.Payload);
                int damageAmount = args["DamageAmount"]?.Value<int>() ?? 0;
                int damageType   = args["DamageType"]?.Value<int>()   ?? 0;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    lp.DamageEntity(damageAmount, damageType);
                    await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx,
                        new JObject(new JProperty("ok", true)).ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
