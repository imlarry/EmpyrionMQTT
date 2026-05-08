using Eleon.Modding;
using ESB.Helpers;
using ESB.Messaging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public partial class ApplicationHandler
    {
        private readonly ContextData _ctx;

        public ApplicationHandler(ContextData ctx) { _ctx = ctx; }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("App/req/GameTicks",           GameTicks);
            _ctx.Messenger.RegisterHandler("App/req/Mode",                Mode);
            _ctx.Messenger.RegisterHandler("App/req/State",               State);
            _ctx.Messenger.RegisterHandler("App/req/ModApiProperties",    ModApiProperties);
            _ctx.Messenger.RegisterHandler("App/req/GetAllPlayfields",    GetAllPlayfields);
            _ctx.Messenger.RegisterHandler("App/req/PfServerInfos",       GetPfServerInfos);
            _ctx.Messenger.RegisterHandler("App/req/PlayerEntityIds",     GetPlayerEntityIds);
            _ctx.Messenger.RegisterHandler("App/req/BlockAndItemMapping", GetBlockAndItemMapping);
            _ctx.Messenger.RegisterHandler("App/req/GetPathFor",          GetPathFor);
            _ctx.Messenger.RegisterHandler("App/req/GetPlayerDataFor",    GetPlayerDataFor);
            _ctx.Messenger.RegisterHandler("App/req/GetStructure",        GetStructure);
            _ctx.Messenger.RegisterHandler("App/req/GetStructures",       GetStructures);
            _ctx.Messenger.RegisterHandler("App/req/SendChatMessage",     SendChatMessage);
            _ctx.Messenger.RegisterHandler("App/req/ShowDialogBox",       ShowDialogBox);
            _ctx.Messenger.RegisterHandler("App/req/Describe",            AppDescribe);
        }

        public async Task AppDescribe(MessageContext ctx)
        {
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx,
                HandlerHelper.ScopeManifestJson("App", _opDefs));
        }

        public async Task GameTicks(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GameTicks", _opDefs["GameTicks"]);
                return;
            }
            var json = new JObject(new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks));
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
        }

        public async Task Mode(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "Mode", _opDefs["Mode"]);
                return;
            }
            var json = new JObject(new JProperty("Mode", _ctx.ModApi.Application.Mode.ToString()));
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
        }

        public async Task State(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "State", _opDefs["State"]);
                return;
            }
            var json = new JObject(new JProperty("State", _ctx.ModApi.Application.State.ToString()));
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
        }

        public async Task ModApiProperties(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "ModApiProperties", _opDefs["ModApiProperties"]);
                return;
            }
            var json = new JObject(
                new JProperty("ClientPlayfield", _ctx.GameManager.CurrentPlayfield != null ? "set" : "null"),
                new JProperty("Network",         _ctx.ModApi.Network          == null ? "null" : "set"),
                new JProperty("GUI",             _ctx.ModApi.GUI              == null ? "null" : "set"),
                new JProperty("PDA",             _ctx.ModApi.PDA              == null ? "null" : "set"),
                new JProperty("Scripting",       _ctx.ModApi.Scripting        == null ? "null" : "set"),
                new JProperty("SoundPlayer",     _ctx.ModApi.SoundPlayer      == null ? "null" : "set"),
                new JProperty("Application",     _ctx.ModApi.Application      == null ? "null" : "set"));
            await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
        }

        public async Task GetAllPlayfields(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetAllPlayfields", _opDefs["GetAllPlayfields"]);
                return;
            }
            try
            {
                var list = new List<Dictionary<string, object>>();
                foreach (var pf in _ctx.ModApi.Application.GetAllPlayfields())
                    list.Add(new Dictionary<string, object>
                    {
                        { "PlayfieldName", pf.PlayfieldName },
                        { "PlayfieldType", pf.PlayfieldType.ToString() },
                        { "IsInstance",    pf.IsInstance }
                    });
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, JsonConvert.SerializeObject(list));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPfServerInfos(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetPfServerInfos", _opDefs["GetPfServerInfos"]);
                return;
            }
            try
            {
                var infos = _ctx.ModApi.Application.GetPfServerInfos();
                if (infos == null)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("GetPfServerInfos returned null"));
                    return;
                }
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, JsonConvert.SerializeObject(infos, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPlayerEntityIds(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetPlayerEntityIds", _opDefs["GetPlayerEntityIds"]);
                return;
            }
            try
            {
                var ids = _ctx.ModApi.Application.GetPlayerEntityIds();
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, JsonConvert.SerializeObject(ids, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetBlockAndItemMapping(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetBlockAndItemMapping", _opDefs["GetBlockAndItemMapping"]);
                return;
            }
            try
            {
                var mapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, JsonConvert.SerializeObject(mapping, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPathFor(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetPathFor", _opDefs["GetPathFor"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                Enum.TryParse(args.GetValue("AppFolder").ToString(), true, out AppFolder appFolder);
                var path = _ctx.ModApi.Application.GetPathFor(appFolder) ?? "N/A";
                var json = new JObject(
                    new JProperty("AppFolder", appFolder.ToString()),
                    new JProperty("Path",      Path.GetFullPath(path)));
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetPlayerDataFor(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetPlayerDataFor", _opDefs["GetPlayerDataFor"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                int? playerEntityId = args.GetValue("PlayerEntityId")?.Value<int>();
                if (!playerEntityId.HasValue)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("PlayerEntityId is required"));
                    return;
                }
                var data = _ctx.ModApi.Application.GetPlayerDataFor(playerEntityId.Value);
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, JsonConvert.SerializeObject(data, MessageHelpers.PascalCaseSettings));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetStructure(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetStructure", _opDefs["GetStructure"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                var entityId = args.GetValue("EntityId").Value<int>();

                async void Callback(GlobalStructureInfo s)
                {
                    var json = HandlerHelper.BuildStructureJson(s);
                    await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, json.ToString(Formatting.None));
                }

                if (!_ctx.ModApi.Application.GetStructure(entityId, Callback))
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("GetStructure request failed - invalid entity ID"));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task GetStructures(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "GetStructures", _opDefs["GetStructures"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                string playfieldName = args.GetValue("PlayfieldName")?.ToString();

                FactionData? factionData = null;
                if (args.ContainsKey("FactionId") && args.ContainsKey("FactionGroup"))
                    factionData = new FactionData
                    {
                        Id    = args.GetValue("FactionId").Value<byte>(),
                        Group = (FactionGroup)args.GetValue("FactionGroup").Value<byte>()
                    };

                EntityType? entityType = null;
                if (args.ContainsKey("EntityType") && Enum.TryParse<EntityType>(args.GetValue("EntityType").ToString(), true, out var et))
                    entityType = et;

                if (string.IsNullOrEmpty(playfieldName) && !factionData.HasValue)
                {
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx,
                        MessageHelpers.ErrorJson("Either PlayfieldName or FactionData (FactionId + FactionGroup) must be specified"));
                    return;
                }

                async void Callback(IEnumerable<GlobalStructureInfo> structures)
                {
                    var array = new JArray();
                    foreach (var s in structures)
                        array.Add(HandlerHelper.BuildStructureJson(s));
                    await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, array.ToString(Formatting.None));
                }

                if (!_ctx.ModApi.Application.GetStructures(playfieldName, factionData, entityType, Callback))
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("GetStructures request failed - check parameters"));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task SendChatMessage(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "SendChatMessage", _opDefs["SendChatMessage"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                var msg = new Eleon.MessageData
                {
                    Text       = args.GetValue("Text")?.ToString() ?? "",
                    Channel    = Enum.TryParse<Eleon.MsgChannel>(args.GetValue("Channel")?.ToString(), true, out var ch)   ? ch   : Eleon.MsgChannel.Global,
                    SenderType = Enum.TryParse<Eleon.SenderType>(args.GetValue("SenderType")?.ToString(), true, out var st) ? st   : Eleon.SenderType.ServerInfo,
                };
                if (args.ContainsKey("SenderEntityId"))      msg.SenderEntityId      = args.GetValue("SenderEntityId").Value<int>();
                if (args.ContainsKey("SenderNameOverride"))  msg.SenderNameOverride  = args.GetValue("SenderNameOverride")?.ToString();
                if (args.ContainsKey("RecipientEntityId"))   msg.RecipientEntityId   = args.GetValue("RecipientEntityId").Value<int>();
                if (args.ContainsKey("IsTextLocaKey"))       msg.IsTextLocaKey       = args.GetValue("IsTextLocaKey").Value<bool>();
                if (args.ContainsKey("Arg1"))                msg.Arg1                = args.GetValue("Arg1")?.ToString();
                if (args.ContainsKey("Arg2"))                msg.Arg2                = args.GetValue("Arg2")?.ToString();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.Application.SendChatMessage(msg);
                    await Task.CompletedTask;
                });
                await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, new JObject(new JProperty("ok", true)).ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task ShowDialogBox(MessageContext ctx)
        {
            if (ctx.ParsedTopic.MetaOperation != null)
            {
                await HandlerHelper.ReplyMetaAsync(_ctx.Messenger, ctx, "ShowDialogBox", _opDefs["ShowDialogBox"]);
                return;
            }
            try
            {
                var args = JObject.Parse(ctx.Payload);
                int playerEntityId = args.GetValue("PlayerEntityId")?.Value<int>() ?? _ctx.ModApi.Application.LocalPlayer.Id;

                var config = new DialogConfig
                {
                    TitleText         = args.GetValue("TitleText")?.ToString()          ?? "Dialog",
                    BodyText          = args.GetValue("BodyText")?.ToString()            ?? "",
                    CloseOnLinkClick  = args.GetValue("CloseOnLinkClick")?.Value<bool>() ?? true,
                    ButtonIdxForEsc   = args.GetValue("ButtonIdxForEsc")?.Value<int>()   ?? -1,
                    ButtonIdxForEnter = args.GetValue("ButtonIdxForEnter")?.Value<int>() ?? -1,
                    MaxChars          = args.GetValue("MaxChars")?.Value<int>()          ?? 0,
                    Placeholder       = args.GetValue("Placeholder")?.ToString(),
                    InitialContent    = args.GetValue("InitialContent")?.ToString()
                };
                if (args.ContainsKey("ButtonTexts") && args.GetValue("ButtonTexts") is JArray btArr)
                    config.ButtonTexts = btArr.ToObject<string[]>();

                int customValue = args.GetValue("CustomValue")?.Value<int>() ?? 0;

                void DialogCallback(int buttonIdx, string linkId, string inputContent, int playerId, int customVal)
                {
                    var response = new JObject(
                        new JProperty("PlayerEntityId", playerId),
                        new JProperty("ButtonIdx",      buttonIdx),
                        new JProperty("LinkId",         linkId        ?? ""),
                        new JProperty("InputContent",   inputContent  ?? ""),
                        new JProperty("CustomValue",    customVal));
                    _ = _ctx.Messenger.SendAsync("App", MessageType.Evt, "DialogResponse", response.ToString(Formatting.None));
                }

                bool displayed = false;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    displayed = _ctx.ModApi.Application.ShowDialogBox(playerEntityId, config, DialogCallback, customValue);
                    await Task.CompletedTask;
                });

                if (displayed)
                    await HandlerHelper.ReplyAsync(_ctx.Messenger, ctx, new JObject(new JProperty("ok", true)).ToString(Formatting.None));
                else
                    await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ErrorJson("Failed to display dialog - invalid player entity ID"));
            }
            catch (Exception ex)
            {
                await HandlerHelper.ReplyErrorAsync(_ctx.Messenger, ctx, MessageHelpers.ExceptionJson(ex));
            }
        }

    }
}
