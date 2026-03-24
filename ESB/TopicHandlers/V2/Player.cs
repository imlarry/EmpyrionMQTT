using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
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
            _ctx.Messenger.RegisterHandler("V2.Player.Teleport", Teleport);
            _ctx.Messenger.RegisterHandler("V2.Player.SteamId",  SteamId);
            _ctx.Messenger.RegisterHandler("V2.Player.Stats",    Stats);
        }

        public async Task Teleport(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                JObject PlayerArgs = JObject.Parse(payload);

                string playfield = PlayerArgs["Playfield"]?.ToString();

                if (PlayerArgs["Pos"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("Pos argument is required"));
                    return;
                }

                if (playfield != null && PlayerArgs["Rot"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("Rot argument is required when Playfield is provided"));
                    return;
                }

                Vector3 pos = MessageHelpers.ParseVec3(PlayerArgs["Pos"]);

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    bool teleport;
                    if (playfield == null)
                    {
                        teleport = localPlayer.Teleport(pos);
                    }
                    else
                    {
                        Vector3 rot = MessageHelpers.ParseVec3(PlayerArgs["Rot"]);
                        teleport = localPlayer.Teleport(playfield, pos, rot);
                    }
                    JObject json = new JObject(new JProperty("Teleport", teleport));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task Stats(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    JToken S(Func<object> getter)
                    {
                        try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); }
                        catch { return JValue.CreateNull(); }
                    }

                    json = new JObject();
                    json.Add("Health",           S(() => localPlayer.Health));
                    json.Add("HealthMax",        S(() => localPlayer.HealthMax));
                    json.Add("Oxygen",           S(() => localPlayer.Oxygen));
                    json.Add("OxygenMax",        S(() => localPlayer.OxygenMax));
                    json.Add("Stamina",          S(() => localPlayer.Stamina));
                    json.Add("StaminaMax",       S(() => localPlayer.StaminaMax));
                    json.Add("Food",             S(() => localPlayer.Food));
                    json.Add("FoodMax",          S(() => localPlayer.FoodMax));
                    json.Add("Radiation",        S(() => localPlayer.Radiation));
                    json.Add("RadiationMax",     S(() => localPlayer.RadiationMax));
                    json.Add("BodyTemp",         S(() => localPlayer.BodyTemp));
                    json.Add("BodyTempMax",      S(() => localPlayer.BodyTempMax));
                    json.Add("Credits",          S(() => localPlayer.Credits));
                    json.Add("ExperiencePoints", S(() => localPlayer.ExperiencePoints));
                    json.Add("UpgradePoints",    S(() => localPlayer.UpgradePoints));
                    json.Add("Kills",            S(() => localPlayer.Kills));
                    json.Add("Died",             S(() => localPlayer.Died));
                    json.Add("Ping",             S(() => localPlayer.Ping));
                    json.Add("HomeBaseId",       S(() => localPlayer.HomeBaseId));
                    json.Add("IsPilot",          S(() => localPlayer.IsPilot));
                    json.Add("FactionData",      S(() => {
                                                     var fd = localPlayer.FactionData;
                                                     return (object)new JObject(
                                                         new JProperty("Group", fd.Group.ToString()),
                                                         new JProperty("Id", fd.Id));
                                                 }));
                    json.Add("FactionRole",      S(() => localPlayer.FactionRole.ToString()));
                    json.Add("CurrentStructureId", S(() => localPlayer.CurrentStructure != null ? (object)localPlayer.CurrentStructure.Id : null));
                    json.Add("DrivingEntityId",  S(() => localPlayer.DrivingEntity != null ? (object)localPlayer.DrivingEntity.Id : null));
                    json.Add("Forward",          S(() => new JObject(
                                                     new JProperty("X", localPlayer.Forward.x),
                                                     new JProperty("Y", localPlayer.Forward.y),
                                                     new JProperty("Z", localPlayer.Forward.z))));
                    json.Add("IsLocal",          S(() => localPlayer.IsLocal));
                    json.Add("IsProxy",          S(() => localPlayer.IsProxy));
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SteamId(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var steamId = localPlayer.SteamId;
                    JObject json = new JObject(new JProperty("SteamId", steamId));
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
