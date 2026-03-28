
using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Application
    {
        private readonly ContextData _ctx;

        public Application(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Application.GetPathFor",          GetPathFor);
            _ctx.Messenger.RegisterHandler("V2.Application.GetAllPlayfields",    GetAllPlayfields);
            _ctx.Messenger.RegisterHandler("V2.Application.GetPfServerInfos",    GetPfServerInfos);
            _ctx.Messenger.RegisterHandler("V2.Application.GetPlayerEntityIds",  GetPlayerEntityIds);
            _ctx.Messenger.RegisterHandler("V2.Application.GetPlayerDataFor",    GetPlayerDataFor);
            _ctx.Messenger.RegisterHandler("V2.Application.SendChatMessage",     SendChatMessage);
            _ctx.Messenger.RegisterHandler("V2.Application.ShowDialogBox",       ShowDialogBox);
            _ctx.Messenger.RegisterHandler("V2.Application.GetStructure",        GetStructure);
            _ctx.Messenger.RegisterHandler("V2.Application.GetStructures",       GetStructures);
            _ctx.Messenger.RegisterHandler("V2.Application.GetBlockAndItemMapping", GetBlockAndItemMapping);
            _ctx.Messenger.RegisterHandler("V2.Application.State",               State);
            _ctx.Messenger.RegisterHandler("V2.Application.Mode",                Mode);
            _ctx.Messenger.RegisterHandler("V2.Application.LocalPlayer",         LocalPlayer);
            _ctx.Messenger.RegisterHandler("V2.Application.GameTicks",           GameTicks);
        }

        public async Task GetPathFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                Enum.TryParse(applicationArgs.GetValue("AppFolder").ToString(), true, out AppFolder appFolder);
                var path = _ctx.ModApi.Application.GetPathFor(appFolder) ?? "N/A";
                JObject json = new JObject(
                    new JProperty("AppFolder", appFolder.ToString()),
                    new JProperty("Path", Path.GetFullPath(path)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetAllPlayfields(string topic, string payload)
        {
            try
            {
                var playfieldDescr = _ctx.ModApi.Application.GetAllPlayfields();
                var playfieldDict = new List<Dictionary<string, object>>();
                foreach (var playfield in playfieldDescr)
                {
                    var data = new Dictionary<string, object>
                    {
                        { "PlayfieldName", playfield.PlayfieldName },
                        { "PlayfieldType", playfield.PlayfieldType },
                        { "IsInstance", playfield.IsInstance }
                    };
                    playfieldDict.Add(data);
                }
                var json = JsonConvert.SerializeObject(playfieldDict);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString());
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPfServerInfos(string topic, string payload)
        {
            try
            {
                var pfServerInfos = _ctx.ModApi.Application.GetPfServerInfos();
                if (pfServerInfos != null)
                {
                    string json = JsonConvert.SerializeObject(pfServerInfos);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);
                }
                else
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("GetPfServerInfos returned null"));
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPlayerEntityIds(string topic, string payload)
        {
            try
            {
                var playerEntityIds = _ctx.ModApi.Application.GetPlayerEntityIds();
                string json = JsonConvert.SerializeObject(playerEntityIds);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPlayerDataFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int? playerEntityId = applicationArgs.GetValue("PlayerEntityId")?.Value<int>();
                if (!playerEntityId.HasValue)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("PlayerEntityId is required"));
                    return;
                }
                var playerData = _ctx.ModApi.Application.GetPlayerDataFor(playerEntityId.Value);
                string json = JsonConvert.SerializeObject(playerData);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SendChatMessage(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);

                var messageData = new Eleon.MessageData()
                {
                    Text = applicationArgs.GetValue("Text")?.ToString() ?? "",
                    Channel = Enum.TryParse<Eleon.MsgChannel>(applicationArgs.GetValue("Channel")?.ToString(), true, out var channel)
                        ? channel
                        : Eleon.MsgChannel.Global,
                    SenderType = Enum.TryParse<Eleon.SenderType>(applicationArgs.GetValue("SenderType")?.ToString(), true, out var senderType)
                        ? senderType
                        : Eleon.SenderType.ServerInfo,
                };

                if (applicationArgs.ContainsKey("SenderEntityId"))
                    messageData.SenderEntityId = applicationArgs.GetValue("SenderEntityId").Value<int>();

                if (applicationArgs.ContainsKey("SenderNameOverride"))
                    messageData.SenderNameOverride = applicationArgs.GetValue("SenderNameOverride")?.ToString();

                if (applicationArgs.ContainsKey("RecipientEntityId"))
                    messageData.RecipientEntityId = applicationArgs.GetValue("RecipientEntityId").Value<int>();

                if (applicationArgs.ContainsKey("IsTextLocaKey"))
                    messageData.IsTextLocaKey = applicationArgs.GetValue("IsTextLocaKey").Value<bool>();

                if (applicationArgs.ContainsKey("Arg1"))
                    messageData.Arg1 = applicationArgs.GetValue("Arg1")?.ToString();

                if (applicationArgs.ContainsKey("Arg2"))
                    messageData.Arg2 = applicationArgs.GetValue("Arg2")?.ToString();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.Application.SendChatMessage(messageData);
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("Message", "Chat message sent successfully")).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task ShowDialogBox(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);

                int playerEntityId = applicationArgs.GetValue("PlayerEntityId")?.Value<int>() ?? _ctx.ModApi.Application.LocalPlayer.Id;

                var config = new Eleon.Modding.DialogConfig()
                {
                    TitleText = applicationArgs.GetValue("TitleText")?.ToString() ?? "Dialog",
                    BodyText = applicationArgs.GetValue("BodyText")?.ToString() ?? "",
                    CloseOnLinkClick = applicationArgs.GetValue("CloseOnLinkClick")?.Value<bool>() ?? true,
                    ButtonIdxForEsc = applicationArgs.GetValue("ButtonIdxForEsc")?.Value<int>() ?? -1,
                    ButtonIdxForEnter = applicationArgs.GetValue("ButtonIdxForEnter")?.Value<int>() ?? -1,
                    MaxChars = applicationArgs.GetValue("MaxChars")?.Value<int>() ?? 0,
                    Placeholder = applicationArgs.GetValue("Placeholder")?.ToString(),
                    InitialContent = applicationArgs.GetValue("InitialContent")?.ToString()
                };

                if (applicationArgs.ContainsKey("ButtonTexts"))
                {
                    var buttonTextsArray = applicationArgs.GetValue("ButtonTexts") as JArray;
                    if (buttonTextsArray != null)
                        config.ButtonTexts = buttonTextsArray.ToObject<string[]>();
                }

                int customValue = applicationArgs.GetValue("CustomValue")?.Value<int>() ?? 0;

                void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customVal)
                {
                    JObject response = new JObject(
                        new JProperty("PlayerEntityId", playerId),
                        new JProperty("ButtonIdx", buttonIdx),
                        new JProperty("LinkId", linkId ?? ""),
                        new JProperty("InputContent", inputContent ?? ""),
                        new JProperty("CustomValue", customVal)
                    );

                    _ = _ctx.Messenger.SendAsync(MessageClass.Event, "V2.Application.DialogResponse", response.ToString(Newtonsoft.Json.Formatting.None));
                }

                bool displayed = false;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    displayed = _ctx.ModApi.Application.ShowDialogBox(playerEntityId, config, DialogActionHandler, customValue);
                    await Task.CompletedTask;
                });

                if (displayed)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("Displayed", true)).ToString(Formatting.None));
                else
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("Failed to display dialog - invalid player entity ID"));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetStructure(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                var entityId = applicationArgs.GetValue("EntityId").Value<int>();

                async void ResultCallback(Eleon.Modding.GlobalStructureInfo s)
                {
                    var json = new JObject(
                        new JProperty("Id",             s.id),
                        new JProperty("Name",           s.name),
                        new JProperty("FactionId",      s.factionId),
                        new JProperty("FactionGroup",   s.factionGroup),
                        new JProperty("ClassNr",        s.classNr),
                        new JProperty("CoreType",       s.coreType),
                        new JProperty("Type",           s.type),
                        new JProperty("PlayfieldName",  s.PlayfieldName),
                        new JProperty("Pos", MessageHelpers.Vec(new Vector3(s.pos.x, s.pos.y, s.pos.z))),
                        new JProperty("Rot", MessageHelpers.Vec(new Vector3(s.rot.x, s.rot.y, s.rot.z))),
                        new JProperty("LastVisitedUtc", s.lastVisitedUTC),
                        new JProperty("Powered",        s.powered),
                        new JProperty("DockedShips",    s.dockedShips != null
                                                            ? (JToken)new JArray(s.dockedShips)
                                                            : JValue.CreateNull())
                    );
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        json.ToString(Newtonsoft.Json.Formatting.None));
                }

                bool requestSent = _ctx.ModApi.Application.GetStructure(entityId, ResultCallback);

                if (!requestSent)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("GetStructure request failed - invalid entity ID"));
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetStructures(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);

                string playfieldName = applicationArgs.GetValue("PlayfieldName")?.ToString();

                FactionData? factionData = null;
                if (applicationArgs.ContainsKey("FactionId") && applicationArgs.ContainsKey("FactionGroup"))
                {
                    factionData = new FactionData
                    {
                        Id = applicationArgs.GetValue("FactionId").Value<byte>(),
                        Group = (FactionGroup)applicationArgs.GetValue("FactionGroup").Value<byte>()
                    };
                }

                EntityType? entityType = null;
                if (applicationArgs.ContainsKey("EntityType"))
                {
                    if (Enum.TryParse<EntityType>(applicationArgs.GetValue("EntityType").ToString(), true, out var et))
                        entityType = et;
                }

                if (string.IsNullOrEmpty(playfieldName) && !factionData.HasValue)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("Either PlayfieldName or FactionData (FactionId + FactionGroup) must be specified"));
                    return;
                }

                async void ResultCallback(IEnumerable<Eleon.Modding.GlobalStructureInfo> structures)
                {
                    var array = new JArray();
                    foreach (var s in structures)
                        array.Add(new JObject(
                            new JProperty("Id",             s.id),
                            new JProperty("Name",           s.name),
                            new JProperty("FactionId",      s.factionId),
                            new JProperty("FactionGroup",   s.factionGroup),
                            new JProperty("ClassNr",        s.classNr),
                            new JProperty("CoreType",       s.coreType),
                            new JProperty("Type",           s.type),
                            new JProperty("PlayfieldName",  s.PlayfieldName),
                            new JProperty("Pos", MessageHelpers.Vec(new Vector3(s.pos.x, s.pos.y, s.pos.z))),
                            new JProperty("Rot", MessageHelpers.Vec(new Vector3(s.rot.x, s.rot.y, s.rot.z))),
                            new JProperty("LastVisitedUtc", s.lastVisitedUTC),
                            new JProperty("Powered",        s.powered),
                            new JProperty("DockedShips",    s.dockedShips != null
                                                                ? (JToken)new JArray(s.dockedShips)
                                                                : JValue.CreateNull())
                        ));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        array.ToString(Newtonsoft.Json.Formatting.None));
                }

                bool requestSent = _ctx.ModApi.Application.GetStructures(playfieldName, factionData, entityType, ResultCallback);

                if (!requestSent)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("GetStructures request failed - check parameters"));
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetBlockAndItemMapping(string topic, string payload)
        {
            try
            {
                var blockAndItemMapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
                var json = JsonConvert.SerializeObject(blockAndItemMapping);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task State(string topic, string payload)
        {
            var state = _ctx.ModApi.Application.State;
            JObject json = new JObject(new JProperty("State", state.ToString()));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task Mode(string topic, string payload)
        {
            var mode = _ctx.ModApi.Application.Mode;
            JObject json = new JObject(new JProperty("Mode", mode.ToString()));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task LocalPlayer(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;

                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("LocalPlayer", null)).ToString(Formatting.None));
                    return;
                }

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    json = new JObject();
                    JToken S(Func<object> getter) { try { var v = getter(); return v == null ? JValue.CreateNull() : JToken.FromObject(v); } catch { return JValue.CreateNull(); } }
                    json.Add("Id",               S(() => localPlayer.Id));
                    json.Add("Name",             S(() => localPlayer.Name));
                    json.Add("Position",         S(() => new JObject(new JProperty("X", localPlayer.Position.x), new JProperty("Y", localPlayer.Position.y), new JProperty("Z", localPlayer.Position.z))));
                    json.Add("Rotation",         S(() => MessageHelpers.Vec(localPlayer.Rotation)));
                    json.Add("Forward",          S(() => new JObject(new JProperty("X", localPlayer.Forward.x),  new JProperty("Y", localPlayer.Forward.y),  new JProperty("Z", localPlayer.Forward.z))));
                    json.Add("IsLocal",          S(() => localPlayer.IsLocal));
                    json.Add("IsProxy",          S(() => localPlayer.IsProxy));
                    json.Add("Type",             S(() => localPlayer.Type.ToString()));
                    json.Add("Health",           S(() => localPlayer.Health));
                    json.Add("HealthMax",        S(() => localPlayer.HealthMax));
                    json.Add("Food",             S(() => localPlayer.Food));
                    json.Add("FoodMax",          S(() => localPlayer.FoodMax));
                    json.Add("Stamina",          S(() => localPlayer.Stamina));
                    json.Add("StaminaMax",       S(() => localPlayer.StaminaMax));
                    json.Add("Oxygen",           S(() => localPlayer.Oxygen));
                    json.Add("OxygenMax",        S(() => localPlayer.OxygenMax));
                    json.Add("BodyTemp",         S(() => localPlayer.BodyTemp));
                    json.Add("BodyTempMax",      S(() => localPlayer.BodyTempMax));
                    json.Add("Radiation",        S(() => localPlayer.Radiation));
                    json.Add("RadiationMax",     S(() => localPlayer.RadiationMax));
                    json.Add("ExperiencePoints", S(() => localPlayer.ExperiencePoints));
                    json.Add("Credits",          S(() => localPlayer.Credits));
                    json.Add("Kills",            S(() => localPlayer.Kills));
                    json.Add("Died",             S(() => localPlayer.Died));
                    json.Add("FactionRole",      S(() => localPlayer.FactionRole.ToString()));
                    json.Add("FactionData",      S(() => new JObject(new JProperty("Id", localPlayer.FactionData.Id), new JProperty("Group", localPlayer.FactionData.Group.ToString()))));
                    json.Add("HomeBaseId",       S(() => localPlayer.HomeBaseId));
                    json.Add("IsPilot",          S(() => localPlayer.IsPilot));
                    json.Add("CurrentStructureId", S(() => localPlayer.CurrentStructure?.Id ?? 0));
                    json.Add("DrivingEntityId",  S(() => localPlayer.DrivingEntity?.Id ?? 0));
                    json.Add("SteamId",          S(() => localPlayer.SteamId));
                    json.Add("SteamOwnerId",     S(() => localPlayer.SteamOwnerId));
                    json.Add("StartPlayfield",   S(() => localPlayer.StartPlayfield));
                    json.Add("Origin",           S(() => localPlayer.Origin));
                    json.Add("Permission",       S(() => localPlayer.Permission));
                    json.Add("Ping",             S(() => localPlayer.Ping));
                    json.Add("UpgradePoints",    S(() => localPlayer.UpgradePoints));
                    json.Add("IsPoi",            S(() => localPlayer.IsPoi));
                    json.Add("BelongsTo",        S(() => localPlayer.BelongsTo));
                    json.Add("DockedTo",         S(() => localPlayer.DockedTo));
                    json.Add("Toolbar",          S(() => localPlayer.Toolbar != null ? (object)JArray.FromObject(localPlayer.Toolbar) : new JArray()));
                    json.Add("Bag",              S(() => localPlayer.Bag != null ? (object)JArray.FromObject(localPlayer.Bag) : new JArray()));
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GameTicks(string topic, string payload)
        {
            var gameTicks = _ctx.ModApi.Application.GameTicks;
            JObject json = new JObject(new JProperty("GameTicks", gameTicks));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
