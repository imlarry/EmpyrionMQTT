using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;
using ESB.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public class AppHandler
    {
        private readonly ContextData _ctx;

        public AppHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Bus.OnRequest("App", "GetProperties",       GetProperties);
            _ctx.Bus.OnRequest("App", "GameTicks",           GameTicks);
            _ctx.Bus.OnRequest("App", "Mode",                Mode);
            _ctx.Bus.OnRequest("App", "State",               State);
            _ctx.Bus.OnRequest("App", "ModApiProperties",    ModApiProperties);
            _ctx.Bus.OnRequest("App", "GetAllPlayfields",    GetAllPlayfields);
            _ctx.Bus.OnRequest("App", "PfServerInfos",       GetPfServerInfos);
            _ctx.Bus.OnRequest("App", "PlayerEntityIds",     GetPlayerEntityIds);
            _ctx.Bus.OnRequest("App", "BlockAndItemMapping", GetBlockAndItemMapping);
            _ctx.Bus.OnRequest("App", "GetPathFor",          GetPathFor);
            _ctx.Bus.OnRequest("App", "GetPlayerDataFor",    GetPlayerDataFor);
            _ctx.Bus.OnRequest("App", "GetStructure",        GetStructureAsync);
            _ctx.Bus.OnRequest("App", "GetStructures",       GetStructuresAsync);
            _ctx.Bus.OnRequest("App", "SendChatMessage",     SendChatMessage);
            _ctx.Bus.OnRequest("App", "ShowDialogBox",       ShowDialogBox);
        }

        // =========================================================================
        // App/GetProperties -- (no payload)
        // Returns all scalar Application properties plus available list data in one response.
        // List fields are omitted (not null) if the underlying API call fails.
        // =========================================================================
        private Task<string> GetProperties(MessageEnvelope env)
        {
            var app = _ctx.ModApi.Application;
            var json = new JObject();
            json["GameTicks"] = app.GameTicks;
            json["Mode"]      = app.Mode.ToString();
            json["State"]     = app.State.ToString();
            json["ModApiProperties"] = new JObject(
                new JProperty("ClientPlayfield", _ctx.GameManager.CurrentPlayfield != null ? "set" : "null"),
                new JProperty("Network",         _ctx.ModApi.Network     == null ? "null" : "set"),
                new JProperty("GUI",             _ctx.ModApi.GUI         == null ? "null" : "set"),
                new JProperty("PDA",             _ctx.ModApi.PDA         == null ? "null" : "set"),
                new JProperty("Scripting",       _ctx.ModApi.Scripting   == null ? "null" : "set"),
                new JProperty("SoundPlayer",     _ctx.ModApi.SoundPlayer == null ? "null" : "set"),
                new JProperty("Application",     _ctx.ModApi.Application == null ? "null" : "set"));
            try
            {
                var list = new List<PlayfieldInfoResponse>();
                foreach (var pf in app.GetAllPlayfields())
                    list.Add(new PlayfieldInfoResponse { PlayfieldName = pf.PlayfieldName, PlayfieldType = pf.PlayfieldType.ToString(), IsInstance = pf.IsInstance });
                json["Playfields"] = JArray.Parse(JsonConvert.SerializeObject(list, MessageHelpers.PascalCaseSettings));
            }
            catch { }
            try
            {
                var ids = app.GetPlayerEntityIds();
                json["PlayerEntityIds"] = ids != null ? (JToken)JArray.Parse(JsonConvert.SerializeObject(ids)) : JValue.CreateNull();
            }
            catch { }
            try
            {
                var infos = app.GetPfServerInfos();
                json["PfServerInfos"] = infos != null ? JToken.Parse(JsonConvert.SerializeObject(infos, MessageHelpers.PascalCaseSettings)) : JValue.CreateNull();
            }
            catch { }
            return Task.FromResult(json.ToString(Newtonsoft.Json.Formatting.None));
        }

        private Task<string> GameTicks(MessageEnvelope env)
        {
            return Task.FromResult(
                new JObject(new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks))
                    .ToString(Formatting.None));
        }

        private Task<string> Mode(MessageEnvelope env)
        {
            return Task.FromResult(
                new JObject(new JProperty("Mode", _ctx.ModApi.Application.Mode.ToString()))
                    .ToString(Formatting.None));
        }

        private Task<string> State(MessageEnvelope env)
        {
            return Task.FromResult(
                new JObject(new JProperty("State", _ctx.ModApi.Application.State.ToString()))
                    .ToString(Formatting.None));
        }

        private Task<string> ModApiProperties(MessageEnvelope env)
        {
            var json = new JObject(
                new JProperty("ClientPlayfield", _ctx.GameManager.CurrentPlayfield != null ? "set" : "null"),
                new JProperty("Network",         _ctx.ModApi.Network     == null ? "null" : "set"),
                new JProperty("GUI",             _ctx.ModApi.GUI         == null ? "null" : "set"),
                new JProperty("PDA",             _ctx.ModApi.PDA         == null ? "null" : "set"),
                new JProperty("Scripting",       _ctx.ModApi.Scripting   == null ? "null" : "set"),
                new JProperty("SoundPlayer",     _ctx.ModApi.SoundPlayer == null ? "null" : "set"),
                new JProperty("Application",     _ctx.ModApi.Application == null ? "null" : "set"));
            return Task.FromResult(json.ToString(Formatting.None));
        }

        private Task<string> GetAllPlayfields(MessageEnvelope env)
        {
            try
            {
                var list = new List<PlayfieldInfoResponse>();
                foreach (var pf in _ctx.ModApi.Application.GetAllPlayfields())
                    list.Add(new PlayfieldInfoResponse
                    {
                        PlayfieldName = pf.PlayfieldName,
                        PlayfieldType = pf.PlayfieldType.ToString(),
                        IsInstance    = pf.IsInstance
                    });
                return Task.FromResult(JsonConvert.SerializeObject(list, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private Task<string> GetPfServerInfos(MessageEnvelope env)
        {
            try
            {
                var infos = _ctx.ModApi.Application.GetPfServerInfos();
                if (infos == null)
                    return Task.FromResult(MessageHelpers.ErrorJson("GetPfServerInfos returned null"));
                return Task.FromResult(JsonConvert.SerializeObject(infos, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private Task<string> GetPlayerEntityIds(MessageEnvelope env)
        {
            try
            {
                var ids = _ctx.ModApi.Application.GetPlayerEntityIds();
                return Task.FromResult(JsonConvert.SerializeObject(ids, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private Task<string> GetBlockAndItemMapping(MessageEnvelope env)
        {
            try
            {
                var mapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
                return Task.FromResult(JsonConvert.SerializeObject(mapping, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private Task<string> GetPathFor(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<GetPathForRequest>();
                if (string.IsNullOrEmpty(req?.AppFolder))
                    return Task.FromResult(MessageHelpers.ErrorJson("AppFolder is required"));
                AppFolder appFolder;
                if (!Enum.TryParse(req.AppFolder, true, out appFolder))
                    return Task.FromResult(MessageHelpers.ErrorJson("Invalid AppFolder value: " + req.AppFolder));
                var raw = _ctx.ModApi.Application.GetPathFor(appFolder) ?? "N/A";
                return Task.FromResult(JsonConvert.SerializeObject(
                    new GetPathForResponse { AppFolder = appFolder.ToString(), Path = Path.GetFullPath(raw) },
                    MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private Task<string> GetPlayerDataFor(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<GetPlayerDataForRequest>();
                if (!req.PlayerEntityId.HasValue)
                    return Task.FromResult(MessageHelpers.ErrorJson("PlayerEntityId is required"));
                var data = _ctx.ModApi.Application.GetPlayerDataFor(req.PlayerEntityId.Value);
                return Task.FromResult(JsonConvert.SerializeObject(data, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        private async Task<string> GetStructureAsync(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<GetStructureRequest>();
                if (!req.EntityId.HasValue)
                    return MessageHelpers.ErrorJson("EntityId is required");
                var tcs = new TaskCompletionSource<string>();
                if (!_ctx.ModApi.Application.GetStructure(req.EntityId.Value,
                        s => tcs.SetResult(HandlerHelper.BuildStructureJson(s).ToString(Formatting.None))))
                    return MessageHelpers.ErrorJson("GetStructure request failed - invalid entity ID");
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        private async Task<string> GetStructuresAsync(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<GetStructuresRequest>();

                FactionData? factionData = null;
                if (req.FactionId.HasValue && req.FactionGroup.HasValue)
                    factionData = new FactionData { Id = req.FactionId.Value, Group = (FactionGroup)req.FactionGroup.Value };

                EntityType? entityType = null;
                if (!string.IsNullOrEmpty(req.EntityType))
                {
                    EntityType et;
                    if (!Enum.TryParse(req.EntityType, true, out et))
                        return MessageHelpers.ErrorJson("Invalid EntityType value: " + req.EntityType);
                    entityType = et;
                }

                if (string.IsNullOrEmpty(req.PlayfieldName) && !factionData.HasValue)
                    return MessageHelpers.ErrorJson("Either PlayfieldName or FactionData (FactionId + FactionGroup) must be specified");

                var tcs = new TaskCompletionSource<string>();
                if (!_ctx.ModApi.Application.GetStructures(req.PlayfieldName, factionData, entityType, structures =>
                {
                    var array = new JArray();
                    foreach (var s in structures)
                        array.Add(HandlerHelper.BuildStructureJson(s));
                    tcs.SetResult(array.ToString(Formatting.None));
                }))
                    return MessageHelpers.ErrorJson("GetStructures request failed - check parameters");
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        private async Task<string> SendChatMessage(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<SendChatMessageRequest>();
                Eleon.MsgChannel ch;
                Eleon.SenderType st;
                var msg = new Eleon.MessageData
                {
                    Text       = req.Text ?? "",
                    Channel    = Enum.TryParse(req.Channel    ?? "", true, out ch) ? ch : Eleon.MsgChannel.Global,
                    SenderType = Enum.TryParse(req.SenderType ?? "", true, out st) ? st : Eleon.SenderType.ServerInfo,
                };
                if (req.SenderEntityId.HasValue)    msg.SenderEntityId     = req.SenderEntityId.Value;
                if (req.SenderNameOverride != null)  msg.SenderNameOverride = req.SenderNameOverride;
                if (req.RecipientEntityId.HasValue)  msg.RecipientEntityId  = req.RecipientEntityId.Value;
                if (req.IsTextLocaKey.HasValue)      msg.IsTextLocaKey      = req.IsTextLocaKey.Value;
                if (req.Arg1 != null)               msg.Arg1               = req.Arg1;
                if (req.Arg2 != null)               msg.Arg2               = req.Arg2;

                return await _ctx.MainThreadRunner.RunOnMainThread(() =>
                {
                    _ctx.ModApi.Application.SendChatMessage(msg);
                    return new JObject(new JProperty("ok", true)).ToString(Formatting.None);
                });
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }

        private async Task<string> ShowDialogBox(MessageEnvelope env)
        {
            try
            {
                var req = env.PayloadAs<ShowDialogBoxRequest>();
                int playerEntityId = req.PlayerEntityId ?? _ctx.ModApi.Application.LocalPlayer.Id;

                var config = new DialogConfig
                {
                    TitleText         = req.TitleText         ?? "Dialog",
                    BodyText          = req.BodyText          ?? "",
                    CloseOnLinkClick  = req.CloseOnLinkClick  ?? true,
                    ButtonIdxForEsc   = req.ButtonIdxForEsc   ?? -1,
                    ButtonIdxForEnter = req.ButtonIdxForEnter ?? -1,
                    MaxChars          = req.MaxChars          ?? 0,
                    Placeholder       = req.Placeholder,
                    InitialContent    = req.InitialContent
                };
                if (req.ButtonTexts != null)
                    config.ButtonTexts = req.ButtonTexts;

                int customValue = req.CustomValue ?? 0;

                void DialogCallback(int buttonIdx, string linkId, string inputContent, int playerId, int customVal)
                {
                    var evt = new JObject(
                        new JProperty("PlayerEntityId", playerId),
                        new JProperty("ButtonIdx",      buttonIdx),
                        new JProperty("LinkId",         linkId       ?? ""),
                        new JProperty("InputContent",   inputContent ?? ""),
                        new JProperty("CustomValue",    customVal));
                    _ = _ctx.Bus.PublishEventAsync("App", "DialogResponse", evt);
                }

                bool displayed = await _ctx.MainThreadRunner.RunOnMainThread(() =>
                    _ctx.ModApi.Application.ShowDialogBox(playerEntityId, config, DialogCallback, customValue));

                return displayed
                    ? new JObject(new JProperty("ok", true)).ToString(Formatting.None)
                    : MessageHelpers.ErrorJson("Failed to display dialog - invalid player entity ID");
            }
            catch (Exception ex)
            {
                return MessageHelpers.ExceptionJson(ex);
            }
        }
    }
}
