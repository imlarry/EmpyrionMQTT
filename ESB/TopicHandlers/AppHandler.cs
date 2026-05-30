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
    public class AppHandler : TopicHandlerBase
    {
        public AppHandler(ContextData ctx) : base(ctx) { }

        private static readonly string[] PlayfieldInfoColumns = { "PlayfieldName", "PlayfieldType", "IsInstance" };

        public void Register()
        {
            _ctx.Bus.OnRequest("App", "GetProperties",       OnMain(GetProperties));
            _ctx.Bus.OnRequest("App", "GameTicks",           OnMain(GameTicks));
            _ctx.Bus.OnRequest("App", "Mode",                OnMain(Mode));
            _ctx.Bus.OnRequest("App", "State",               OnMain(State));
            _ctx.Bus.OnRequest("App", "ModApiProperties",    OnMain(ModApiProperties));
            _ctx.Bus.OnRequest("App", "GetAllPlayfields",    OnMain(GetAllPlayfields));
            _ctx.Bus.OnRequest("App", "PfServerInfos",       OnMain(GetPfServerInfos));
            _ctx.Bus.OnRequest("App", "PlayerEntityIds",     OnMain(GetPlayerEntityIds));
            _ctx.Bus.OnRequest("App", "BlockAndItemMapping", OnMain(GetBlockAndItemMapping));
            _ctx.Bus.OnRequest("App", "GetPathFor",          OnMain(GetPathFor));
            _ctx.Bus.OnRequest("App", "GetPlayerDataFor",    OnMain(GetPlayerDataFor));
            _ctx.Bus.OnRequest("App", "GetStructure",        OnMain(GetStructureAsync));
            _ctx.Bus.OnRequest("App", "GetStructures",       OnMain(GetStructuresAsync));
            _ctx.Bus.OnRequest("App", "SendChatMessage",     OnMain(SendChatMessage));
            _ctx.Bus.OnRequest("App", "ShowDialogBox",       OnMain(ShowDialogBox));
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
                var rows = new JArray();
                foreach (var pf in app.GetAllPlayfields())
                    rows.Add(new JArray(pf.PlayfieldName, pf.PlayfieldType.ToString(), pf.IsInstance));
                json["Playfields"] = MessageHelpers.Tabular(PlayfieldInfoColumns, rows);
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

        // =========================================================================
        // Scalar property handlers
        // =========================================================================
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

        // =========================================================================
        // App/ModApiProperties -- (no payload)
        // Reports presence ("set"/"null") of each top-level IModApi sub-API.
        // =========================================================================
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

        // =========================================================================
        // App/GetAllPlayfields -- (no payload)
        // Returns array of { PlayfieldName, PlayfieldType, IsInstance }.
        // =========================================================================
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

        // =========================================================================
        // App/PfServerInfos -- (no payload)
        // Returns raw playfield server info objects from IModApi.Application.
        // =========================================================================
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

        // =========================================================================
        // App/PlayerEntityIds -- (no payload)
        // Returns int[] of currently online player entity IDs.
        // =========================================================================
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

        // =========================================================================
        // App/BlockAndItemMapping -- (no payload)
        // Returns the engine's block-and-item name/id mapping table.
        // =========================================================================
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

        // =========================================================================
        // App/GetPathFor -- { "AppFolder": string }
        // AppFolder is parsed against the AppFolder enum (case-insensitive).
        // Returns: { "AppFolder": string, "Path": string }  (absolute path; "N/A" if unknown)
        // =========================================================================
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

        // =========================================================================
        // App/GetPlayerDataFor -- { "PlayerEntityId": int }
        // Returns the engine's PlayerData record for the given entity id.
        // =========================================================================
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

        // =========================================================================
        // App/GetStructure -- { "EntityId": int }
        // Async callback pattern; resolves with structure JSON built by HandlerHelper.
        // Returns: structure object as JSON (see HandlerHelper.BuildStructureJson).
        // =========================================================================
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

        // =========================================================================
        // App/GetStructures -- { "PlayfieldName": string?, "FactionId": int?, "FactionGroup": int?, "EntityType": string? }
        // Either PlayfieldName or both FactionId+FactionGroup must be supplied.
        // Async callback pattern; resolves with JArray of structure objects.
        // Returns: [ structureJson, ... ] (see HandlerHelper.BuildStructureJson)
        // =========================================================================
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

        // =========================================================================
        // App/SendChatMessage -- { "Text": string, "Channel": string?, "SenderType": string?,
        //                          "SenderEntityId": int?, "SenderNameOverride": string?,
        //                          "RecipientEntityId": int?, "IsTextLocaKey": bool?,
        //                          "Arg1": string?, "Arg2": string? }
        // Channel/SenderType default to Global/ServerInfo when missing or unparseable.
        // Dispatched on the main thread.
        // Returns: { "ok": true }
        // =========================================================================
        private Task<string> SendChatMessage(MessageEnvelope env)
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

                _ctx.ModApi.Application.SendChatMessage(msg);
                return Task.FromResult(new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }

        // =========================================================================
        // App/ShowDialogBox -- { "PlayerEntityId": int?, "TitleText": string?, "BodyText": string?,
        //                        "CloseOnLinkClick": bool?, "ButtonIdxForEsc": int?, "ButtonIdxForEnter": int?,
        //                        "MaxChars": int?, "Placeholder": string?, "InitialContent": string?,
        //                        "ButtonTexts": string[]?, "CustomValue": int? }
        // PlayerEntityId defaults to LocalPlayer. Dispatched on the main thread.
        // Player response is published as event App/DialogResponse with:
        //   { PlayerEntityId, ButtonIdx, LinkId, InputContent, CustomValue }
        // Returns: { "ok": true } on display, error JSON on failure.
        // =========================================================================
        private Task<string> ShowDialogBox(MessageEnvelope env)
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
                    var dlgRcId = _ctx.GameManager.GameRcId ?? _ctx.Bus.ContextRcId;
                    _ = _ctx.Bus.PublishEventAsync(dlgRcId, "App", "DialogResponse", evt);
                }

                bool displayed = _ctx.ModApi.Application.ShowDialogBox(playerEntityId, config, DialogCallback, customValue);

                return Task.FromResult(displayed
                    ? new JObject(new JProperty("ok", true)).ToString(Formatting.None)
                    : MessageHelpers.ErrorJson("Failed to display dialog - invalid player entity ID"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
