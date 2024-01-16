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
using System.Diagnostics;

namespace ESB.TopicHandlers
{
    public class Application 
    {
        private readonly ContextData _ctx;

        public Application(ContextData ctx)
        {
            _ctx = ctx;
        }

        public async Task Subscribe()
        {
            await _ctx.Messenger.SubscribeAsync("Application.Teleport", Teleport);
            await _ctx.Messenger.SubscribeAsync("Application.DumpMemory", DumpMemory);
            await _ctx.Messenger.SubscribeAsync("Application.WindowInfo", WindowInfo);
            await _ctx.Messenger.SubscribeAsync("Application.TraceEntity", TraceEntity);
            await _ctx.Messenger.SubscribeAsync("Application.ShowEntity", ShowEntity);
            await _ctx.Messenger.SubscribeAsync("Application.GetPathFor", GetPathFor);
            await _ctx.Messenger.SubscribeAsync("Application.GetAllPlayfields", GetAllPlayfields);
            await _ctx.Messenger.SubscribeAsync("Application.GetPfServerInfos", GetPfServerInfos);
            await _ctx.Messenger.SubscribeAsync("Application.GetPlayerEntityIds", GetPlayerEntityIds);
            await _ctx.Messenger.SubscribeAsync("Application.GetPlayerDataFor", GetPlayerDataFor);
            await _ctx.Messenger.SubscribeAsync("Application.SendChatMessage", SendChatMessage);
            await _ctx.Messenger.SubscribeAsync("Application.ShowDialogBox", ShowDialogBox);
            await _ctx.Messenger.SubscribeAsync("Application.GetStructure", GetStructure);
            await _ctx.Messenger.SubscribeAsync("Application.GetStructures", GetStructures);
            await _ctx.Messenger.SubscribeAsync("Application.GetBlockAndItemMapping", GetBlockAndItemMapping);
            await _ctx.Messenger.SubscribeAsync("Application.State", State);
            await _ctx.Messenger.SubscribeAsync("Application.Mode", Mode);
            await _ctx.Messenger.SubscribeAsync("Application.LocalPlayer", LocalPlayer);
            await _ctx.Messenger.SubscribeAsync("Application.GameTicks", GameTicks);
            await _ctx.Messenger.SubscribeAsync("Application.Player_GetInventory", Player_GetInventory); 
        }

        public async Task Teleport(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                //var playfield = args.GetValue("Playfield").ToString();
                //string posStr = args.GetValue("Pos").ToString();
                //string rotStr = args.GetValue("Rot").ToString();
                //string[] values = posStr.Split(',');
                //Vector3 pos = new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]));
                string playfield = "Adedirha";
                Vector3 pos = new Vector3(500.0f, 500.0f, 500.0f);
                Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
                JObject json = new JObject(
                    new JProperty("ReadPlayfield", playfield),
                    new JProperty("ReadPos", pos.ToString()),
                    new JProperty("ReadRot", rot.ToString())
                    );
                await _ctx.Messenger.SendAsync(MessageClass.Information, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                //var bret = _ctx.ModApi.Application.LocalPlayer.Teleport(pos);
                var bret = _ctx.ModApi.Application.LocalPlayer.Teleport(playfield, pos, rot);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, bret.ToString()); // json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        // Message Entry Points
        public async Task DumpMemory(string topic, string payload)
        {
            try
            {
                var memoryDumper = new MemoryDumper();
                string processName = Process.GetCurrentProcess().ProcessName;
                string dumpFileName = memoryDumper.Dump(processName);
                JObject json = new JObject(new JProperty("DumpFileName", dumpFileName));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task WindowInfo(string topic, string payload)
        {
            try
            {
                WinInfo windownInfo = new WinInfo();
                JObject json = JObject.FromObject(windownInfo);
                await _ctx.Messenger.SendAsync(MessageClass.Request, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task TraceEntity(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int entityId = Convert.ToInt32(applicationArgs.GetValue("EntityId"));
                int duration = Convert.ToInt32(applicationArgs.GetValue("Duration"));
                int refreshRate = Convert.ToInt32(applicationArgs.GetValue("RefreshRate"));

                // Start a new task for the entity
                _ = Task.Run(async () =>
                {
                    var entity = _ctx.GetEntityByKey(entityId);
                    if (entity != null)
                    {
                        DateTime startTime = DateTime.Now;

                        while ((DateTime.Now - startTime).TotalSeconds < duration)
                        {
                            Vector3 position = entity.Position;
                            JObject json = new JObject(
                                new JProperty("Position", new JObject(
                                    new JProperty("X", position.x),
                                    new JProperty("Y", position.y),
                                    new JProperty("Z", position.z)
                                ))
                            );
                            await _ctx.Messenger.SendAsync(MessageClass.Request, topic, json.ToString(Newtonsoft.Json.Formatting.None));

                            // Wait for the refresh interval before the next update
                            await Task.Delay(TimeSpan.FromSeconds(refreshRate));
                        }

                        // Send a final message indicating that the trace has expired
                        await _ctx.Messenger.SendAsync(MessageClass.Information, topic, $"TraceExpired for EntityId: {entityId}");
                    }
                    else
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, "Entity not found");
                    }
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task ShowEntity(string topic, string payload)
        {
            JObject applicationArgs = JObject.Parse(payload);
            int entityId = Convert.ToInt32(applicationArgs.GetValue("EntityId"));
            var entity = _ctx.GetEntityByKey(entityId);

            JObject json = new JObject(
                new JProperty("Id", entity.Id),
                new JProperty("Name", entity.Name),
                new JProperty("Faction", entity.Faction.ToString()),
                new JProperty("Position", new JObject(
                    new JProperty("X", entity.Position.x),
                    new JProperty("Y", entity.Position.y),
                    new JProperty("Z", entity.Position.z)
                )),
                new JProperty("Forward", entity.Forward.ToString()),
                new JProperty("Rotation", entity.Rotation.ToString()),
                new JProperty("IsLocal", entity.IsLocal),
                new JProperty("IsProxy", entity.IsProxy),
                new JProperty("IsPoi", entity.IsPoi),
                new JProperty("BelongsTo", entity.BelongsTo),
                new JProperty("DockedTo", entity.DockedTo),
                new JProperty("Type", entity.Type)
            );

            if (entity.Structure != null)
            {
                JObject structureJson = new JObject(
                    new JProperty("MinPos", entity.Structure.MinPos.ToString()),
                    new JProperty("MaxPos", entity.Structure.MaxPos.ToString()),
                    new JProperty("Id", entity.Structure.Id),
                    new JProperty("IsReady", entity.Structure.IsReady),
                    new JProperty("IsPowered", entity.Structure.IsPowered),
                    new JProperty("IsOfflineProtectable", entity.Structure.IsOfflineProtectable),
                    new JProperty("DamageLevel", entity.Structure.DamageLevel),   // divide by zero error on entity load (before "Ready = true")
                    new JProperty("BlockCount", entity.Structure.BlockCount),
                    new JProperty("DeviceCount", entity.Structure.DeviceCount),
                    new JProperty("LightCount", entity.Structure.LightCount),
                    new JProperty("TriangleCount", entity.Structure.TriangleCount),
                    new JProperty("Fuel", entity.Structure.Fuel),
                    new JProperty("PowerOutCapacity", entity.Structure.PowerOutCapacity),
                    new JProperty("PowerConsumption", entity.Structure.PowerConsumption),
                    new JProperty("PlayerCreatedSteamId", entity.Structure.PlayerCreatedSteamId),
                    new JProperty("CoreType", entity.Structure.CoreType.ToString()),
                    new JProperty("SizeClass", entity.Structure.SizeClass),
                    new JProperty("IsShieldActive", entity.Structure.IsShieldActive),
                    new JProperty("ShieldLevel", entity.Structure.ShieldLevel),
                    new JProperty("TotalMass", entity.Structure.TotalMass),
                    new JProperty("HasLandClaimDevice", entity.Structure.HasLandClaimDevice),
                    new JProperty("LastVisitedTicks", entity.Structure.LastVisitedTicks)
                );
                json.Add(new JProperty("Structure", structureJson));
            }
            else
            {
                json.Add(new JProperty("Structure", null));
            }

            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task Player_GetInventory(string topic, string payload)
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
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }


        /// <summary>
        /// The reply to a GetPathFor message is the path for specific game sub-directories based on the "AppFolder" property 
        /// in the JSON payload. This value must be from the AppFolder enum which includes Root, Content, SaveGame, Mod, ActiveScenario,
        /// Cache, and Dedicated. The use of a /../ parent directory reference and the switch to forward slashes, which do not need 
        /// to be escaped, implies these paths are derived from the System.AppDomain.CurrentDomain.BaseDirectory of the appropriate
        /// Main executable in either the Client, DedicatedServer, or PlayfieldServer directory.
        /// 
        /// Note: These paths contain the game name which include spaces. Subsequent calls using the returned path may require the use
        /// of enclosing quotes to ensure a valid path.
        /// </summary>
        public async Task GetPathFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                Enum.TryParse(applicationArgs.GetValue("AppFolder").ToString(), true, out AppFolder appFolder);
                var path = _ctx.ModApi.Application.GetPathFor(appFolder);
                JObject json = new JObject(new JProperty("Path", Path.GetFullPath(path)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
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
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString());     // TODO: confirm format
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
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
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);     // TODO: confirm format
                }
                else
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "call returned null");     // TODO: json format
                }

            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task GetPlayerEntityIds(string topic, string payload)
        {
            try
            {
                var playerEntityIds = _ctx.ModApi.Application.GetPlayerEntityIds();
                string json = JsonConvert.SerializeObject(playerEntityIds);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);         // TODO: confirm format
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task GetPlayerDataFor(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int? playerEntityId = applicationArgs.GetValue("PlayerEntityId")?.Value<int>() ?? null;
                if (playerEntityId.HasValue)
                {
                    var playerData = _ctx.ModApi.Application.GetPlayerDataFor(playerEntityId.Value);
                    string json = JsonConvert.SerializeObject(playerData);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task SendChatMessage(string topic, string payload)
        {
            try
            {
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "stub");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        void DialogActionHandler(int buttonIdx, string linkId, string inputContent, int playerId, int customValue)
        {
            //_ = _ctx.Messenger.SendAsync("Application.ShowDialogBox/X", "Button:" + buttonIdx.ToString());
            _ctx.ModApi.Log("entering ShowDialogBox actionRoutine");
        }
        public async Task ShowDialogBox(string topic, string payload)
        {
            //try
            //{
                _ctx.ModApi.Log("entering ShowDialogBox");
                var playerId = _ctx.ModApi.Application.LocalPlayer.Id;
                //var playerData = _ctx.ModApi.Application.GetPlayerDataFor(playerId);
                //string json = JsonConvert.SerializeObject(playerData);
                //await _ctx.Messenger.SendAsync(topic, json, MessageClass.Information);
                string[] bt = { "dog", "cat", "duck" };
                var config = new DialogConfig() // the parens here forces calling the constructor (which probably populates stuff behind the curtain!)
                {
                    TitleText = "TitleText",
                    BodyText = "BodyText",
                    ButtonTexts = bt,
                    ButtonIdxForEsc = 0,
                    ButtonIdxForEnter = 1,
                    CloseOnLinkClick = true,
                    MaxChars = 30,
                    Placeholder = "Placeholder",
                    InitialContent = "InitialContent"
                };
                var displayed = _ctx.ModApi.Application.ShowDialogBox(playerId, config, DialogActionHandler, 0);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "Displayed: " + displayed.ToString());
            //}
            //catch (Exception ex)
            //{
            //    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            //}
        }

        public async Task GetStructure(string topic, string payload)
        {
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                var entityId = applicationArgs.GetValue("EntityId").Value<int>();
                async void ResultCallback(GlobalStructureInfo globalStructureInfo)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, globalStructureInfo.name);         // TODO: confirm format
                }
                _ctx.ModApi.Application.GetStructure(entityId, ResultCallback);
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task GetStructures(string topic, string payload)
        {
            try
            {
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "stub");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

        public async Task GetBlockAndItemMapping(string topic, string payload)
        {
            try
            {
                var blockAndItemMapping = _ctx.ModApi.Application.GetBlockAndItemMapping();
                var json = JsonConvert.SerializeObject(blockAndItemMapping);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json);      // TODO: confirm format
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
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
            var localPlayer = _ctx.ModApi.Application.LocalPlayer;
            JObject json = new JObject(new JProperty("Stubbed LocalPlayer.Id", localPlayer.Id));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task GameTicks(string topic, string payload)
        {
            var gameTicks = _ctx.ModApi.Application.GameTicks;
            JObject json = new JObject(new JProperty("GameTicks", gameTicks));
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
        }

    }
}
