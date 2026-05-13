using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using ESB.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class PlayerHandler
    {
        private readonly ContextData _ctx;

        public PlayerHandler(ContextData ctx) { _ctx = ctx; }

        static JObject FD(FactionData fd) => new JObject(new JProperty("Group", fd.Group.ToString()), new JProperty("Id", fd.Id));

        public void Register()
        {
            _ctx.Bus.OnRequest("Player", "GetProperties", Properties);
            _ctx.Bus.OnRequest("Player", "Teleport",      Teleport);
            _ctx.Bus.OnRequest("Player", "DamageEntity",  DamageEntity);
        }

        public async Task<string> Properties(MessageEnvelope env)
        {
            try
            {
                IPlayer player;
                var args     = env.PayloadJson;
                var entityId = args?["EntityId"];
                if (entityId != null && entityId.Type != JTokenType.Null)
                {
                    int id = (int)entityId;
                    var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
                    if (pf == null)
                        return MessageHelpers.ErrorJson("No active playfield");
                    if (!pf.Players.TryGetValue(id, out player) || player == null)
                        return MessageHelpers.ErrorJson("Player entity " + id + " not found on current playfield");
                }
                else
                {
                    player = _ctx.ModApi.Application.LocalPlayer;
                    if (player == null)
                        return MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode");
                }

                return await _ctx.MainThreadRunner.RunOnMainThread(() =>
                {
                    JToken S(Func<object> getter)
                    {
                        try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                        catch { return JValue.CreateNull(); }
                    }

                    var obj = new JObject();
                    obj["Id"]              = S(() => player.Id);
                    obj["Name"]            = S(() => player.Name);
                    obj["SteamId"]         = S(() => player.SteamId);
                    obj["SteamOwnerId"]    = S(() => player.SteamOwnerId);
                    obj["StartPlayfield"]  = S(() => player.StartPlayfield);
                    obj["Origin"]          = S(() => player.Origin);
                    obj["Permission"]      = S(() => player.Permission);
                    obj["Health"]          = S(() => player.Health);
                    obj["HealthMax"]       = S(() => player.HealthMax);
                    obj["Oxygen"]          = S(() => player.Oxygen);
                    obj["OxygenMax"]       = S(() => player.OxygenMax);
                    obj["Stamina"]         = S(() => player.Stamina);
                    obj["StaminaMax"]      = S(() => player.StaminaMax);
                    obj["Food"]            = S(() => player.Food);
                    obj["FoodMax"]         = S(() => player.FoodMax);
                    obj["Radiation"]       = S(() => player.Radiation);
                    obj["RadiationMax"]    = S(() => player.RadiationMax);
                    obj["BodyTemp"]        = S(() => player.BodyTemp);
                    obj["BodyTempMax"]     = S(() => player.BodyTempMax);
                    obj["Credits"]         = S(() => player.Credits);
                    obj["ExperiencePoints"]= S(() => player.ExperiencePoints);
                    obj["UpgradePoints"]   = S(() => player.UpgradePoints);
                    obj["Kills"]           = S(() => player.Kills);
                    obj["Died"]            = S(() => player.Died);
                    obj["Ping"]            = S(() => player.Ping);
                    obj["HomeBaseId"]      = S(() => player.HomeBaseId);
                    obj["IsPilot"]         = S(() => player.IsPilot);
                    obj["IsLocal"]         = S(() => player.IsLocal);
                    obj["IsProxy"]         = S(() => player.IsProxy);
                    obj["IsPoi"]           = S(() => player.IsPoi);
                    obj["BelongsTo"]       = S(() => player.BelongsTo);
                    obj["DockedTo"]        = S(() => player.DockedTo);
                    obj["Type"]            = S(() => player.Type.ToString());
                    obj["FactionData"]     = S(() => FD(player.FactionData));
                    obj["Faction"]         = S(() => FD(player.Faction));
                    obj["FactionRole"]     = S(() => player.FactionRole.ToString());
                    obj["CurrentStructure"] = player.CurrentStructure != null
                        ? (JToken)new JObject(
                            new JProperty("Id",       player.CurrentStructure.Id),
                            new JProperty("EntityId", player.CurrentStructure.Entity.Id))
                        : JValue.CreateNull();
                    obj["DrivingEntity"] = player.DrivingEntity != null
                        ? (JToken)new JObject(new JProperty("EntityId", player.DrivingEntity.Id))
                        : JValue.CreateNull();
                    obj["Position"] = MessageHelpers.Vec(player.Position);
                    obj["Forward"]  = MessageHelpers.Vec(player.Forward);
                    obj["Rotation"] = MessageHelpers.Vec(player.Rotation);
                    obj["Toolbar"]  = HandlerHelper.ItemStacksJson(player.Toolbar);
                    obj["Bag"]      = HandlerHelper.ItemStacksJson(player.Bag);
                    return obj.ToString(Formatting.None);
                });
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        public async Task<string> Teleport(MessageEnvelope env)
        {
            try
            {
                var lp = _ctx.ModApi.Application.LocalPlayer;
                if (lp == null)
                    return MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode");

                JObject args = env.PayloadJson;
                string playfield = args["Playfield"]?.ToString();

                if (args["Pos"] == null)
                    return MessageHelpers.ErrorJson("Pos argument is required");
                if (playfield != null && args["Rot"] == null)
                    return MessageHelpers.ErrorJson("Rot argument is required when Playfield is provided");

                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                return await _ctx.MainThreadRunner.RunOnMainThread(() =>
                {
                    bool result = playfield == null
                        ? lp.Teleport(pos)
                        : lp.Teleport(playfield, pos, MessageHelpers.ParseVec3(args["Rot"]));
                    return new JObject(new JProperty("ok", result)).ToString(Formatting.None);
                });
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        public async Task<string> DamageEntity(MessageEnvelope env)
        {
            try
            {
                var lp = _ctx.ModApi.Application.LocalPlayer;
                if (lp == null)
                    return MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode");

                var req = env.PayloadAs<DamageEntityRequest>();
                return await _ctx.MainThreadRunner.RunOnMainThread(() =>
                {
                    lp.DamageEntity(req.DamageAmount, req.DamageType);
                    return new JObject(new JProperty("ok", true)).ToString(Formatting.None);
                });
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }
    }
}
