using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public class PlayerHandler : TopicHandlerBase
    {
        public PlayerHandler(ContextData ctx) : base(ctx) { }

        public void Register()
        {
            _ctx.Bus.OnRequest("Player", "GetProperties", OnMain(Properties));
            _ctx.Bus.OnRequest("Player", "Teleport",      OnMain(Teleport));
        }

        // =========================================================================
        // Player/GetProperties -- { "EntityId": int? }
        // If EntityId is supplied, resolves that player on the current playfield;
        // otherwise falls back to LocalPlayer. All IPlayer scalars are read on the
        // main thread; individual getters that throw are emitted as null.
        // Returns: object of player scalars plus Position/Forward/Rotation,
        //          CurrentStructure/DrivingEntity refs, Toolbar/Bag stacks.
        // =========================================================================
        public Task<string> Properties(MessageEnvelope env)
        {
            try
            {
                var args     = env.PayloadJson;
                var entityId = args?["EntityId"];

                IPlayer player;
                if (entityId != null && entityId.Type != JTokenType.Null)
                {
                    int id = (int)entityId;
                    var pf = _ctx.GameManager.CurrentPlayfield ?? _ctx.ModApi.ClientPlayfield;
                    if (pf == null)
                        return Task.FromResult(MessageHelpers.ErrorJson("No active playfield"));
                    if (!pf.Players.TryGetValue(id, out player) || player == null)
                        return Task.FromResult(MessageHelpers.ErrorJson("Player entity " + id + " not found on current playfield"));
                }
                else
                {
                    player = _ctx.ModApi.Application.LocalPlayer;
                    if (player == null)
                        return Task.FromResult(MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode"));
                }

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
                obj["FactionData"]     = S(() => HandlerHelper.FactionDataJson(player.FactionData));
                obj["Faction"]         = S(() => HandlerHelper.FactionDataJson(player.Faction));
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
                return Task.FromResult(obj.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // Player/Teleport -- { "Pos": {X,Y,Z}, "Playfield": string?, "Rot": {X,Y,Z}? }
        // Same-playfield form: Pos only. Cross-playfield form: Playfield + Pos + Rot.
        // Dispatched on the main thread; operates on LocalPlayer.
        // Returns: { "ok": bool }
        // =========================================================================
        public Task<string> Teleport(MessageEnvelope env)
        {
            try
            {
                JObject args = env.PayloadJson;
                string playfield = args["Playfield"]?.ToString();

                if (args["Pos"] == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Pos argument is required"));
                if (playfield != null && args["Rot"] == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("Rot argument is required when Playfield is provided"));

                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                var lp = _ctx.ModApi.Application.LocalPlayer;
                if (lp == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("LocalPlayer is null -- no active player in this game mode"));
                bool result = playfield == null
                    ? lp.Teleport(pos)
                    : lp.Teleport(playfield, pos, MessageHelpers.ParseVec3(args["Rot"]));
                return Task.FromResult(new JObject(new JProperty("ok", result)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

    }
}
