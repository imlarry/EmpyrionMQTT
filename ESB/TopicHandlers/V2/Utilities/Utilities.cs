using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Utilities : IUtilities
    {
        private readonly ContextData _ctx;

        public Utilities(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Utilities.TestSelf",    TestSelf);
            _ctx.Messenger.RegisterHandler("V2.Utilities.Teleport",    Teleport);
            _ctx.Messenger.RegisterHandler("V2.Utilities.DumpMemory",  DumpMemory);
            _ctx.Messenger.RegisterHandler("V2.Utilities.WindowInfo",  WindowInfo);
            _ctx.Messenger.RegisterHandler("V2.Utilities.TraceEntity", TraceEntity);
            _ctx.Messenger.RegisterHandler("V2.Utilities.ShowEntity",  ShowEntity);
        }

        public async Task TestSelf(string topic, string payload)
        {
            await _ctx.Messenger.SendAsync(MessageClass.Response, topic, new JObject(new JProperty("Message", "Edna_Selftest OK")).ToString(Newtonsoft.Json.Formatting.None));
        }

        public async Task Teleport(string topic, string payload)
        {
            try
            {
                JObject args = JObject.Parse(payload);
                string playfield = args.GetValue("Playfield").ToString();
                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
                Vector3 rot = MessageHelpers.ParseVec3(args["Rot"]);
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                    if (localPlayer == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("LocalPlayer is null - Teleport is only valid on a client mod with an active player"));
                        return;
                    }
                    var bret = localPlayer.Teleport(playfield, pos, rot);
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, bret.ToString());
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

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
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        public async Task WindowInfo(string topic, string payload)
        {
            try
            {
                WinInfo windownInfo = new WinInfo();
                JObject json = JObject.FromObject(windownInfo);
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
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

                // Start a background trace loop - position reads happen on the main thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_ctx.GetEntityByKey(entityId) == null)
                        {
                            await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                            return;
                        }

                        DateTime startTime = DateTime.Now;
                        while ((DateTime.Now - startTime).TotalSeconds < duration)
                        {
                            // Re-check cache each tick - entity may have been killed/unloaded
                            var entity = _ctx.GetEntityByKey(entityId);
                            if (entity == null)
                            {
                                await _ctx.Messenger.SendAsync(MessageClass.Event, topic,
                                    new JObject(new JProperty("Status", "EntityLost"), new JProperty("EntityId", entityId)).ToString(Formatting.None));
                                return;
                            }

                            // Read position on main thread - Unity objects require it
                            Vector3 position = default;
                            await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                            {
                                position = entity.Position;
                                await Task.CompletedTask;
                            });

                            JObject json = new JObject(
                                new JProperty("EntityId", entityId),
                                new JProperty("Position", new JObject(
                                    new JProperty("X", position.x),
                                    new JProperty("Y", position.y),
                                    new JProperty("Z", position.z)
                                ))
                            );
                            await _ctx.Messenger.SendAsync(MessageClass.Event, topic, json.ToString(Formatting.None));

                            await Task.Delay(TimeSpan.FromSeconds(refreshRate));
                        }

                        await _ctx.Messenger.SendAsync(MessageClass.Information, topic,
                            new JObject(new JProperty("Status", "TraceExpired"), new JProperty("EntityId", entityId)).ToString(Formatting.None));
                    }
                    catch (Exception ex)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
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
            try
            {
                JObject applicationArgs = JObject.Parse(payload);
                int entityId = Convert.ToInt32(applicationArgs.GetValue("EntityId"));
                var entity = _ctx.GetEntityByKey(entityId);

                if (entity == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                    return;
                }

                string factionStr;
                try { factionStr = entity.Faction.ToString(); } catch { factionStr = null; }

                JObject json = new JObject(
                    new JProperty("Id", entity.Id),
                    new JProperty("Name", entity.Name),
                    new JProperty("Faction", factionStr),
                    new JProperty("Position", new JObject(
                        new JProperty("X", entity.Position.x),
                        new JProperty("Y", entity.Position.y),
                        new JProperty("Z", entity.Position.z)
                    )),
                    new JProperty("Forward", MessageHelpers.Vec(entity.Forward)),
                    new JProperty("Rotation", MessageHelpers.Vec(entity.Rotation)),
                    new JProperty("IsLocal", entity.IsLocal),
                    new JProperty("IsProxy", entity.IsProxy),
                    new JProperty("IsPoi", entity.IsPoi),
                    new JProperty("BelongsTo", entity.BelongsTo),
                    new JProperty("DockedTo", entity.DockedTo),
                    new JProperty("Type", entity.Type.ToString())
                );

                if (entity.Structure != null)
                {
                    var s = entity.Structure;
                    JObject structureJson = new JObject(
                        new JProperty("Id", s.Id),
                        new JProperty("IsReady", s.IsReady),
                        new JProperty("MinPos", MessageHelpers.Vec(s.MinPos)),
                        new JProperty("MaxPos", MessageHelpers.Vec(s.MaxPos)),
                        new JProperty("PlayerCreatedSteamId", s.PlayerCreatedSteamId),
                        new JProperty("CoreType", s.CoreType.ToString()),
                        new JProperty("SizeClass", s.SizeClass),
                        new JProperty("LastVisitedTicks", s.LastVisitedTicks)
                    );
                    if (s.IsReady)
                    {
                        structureJson.Add(new JProperty("IsPowered", s.IsPowered));
                        structureJson.Add(new JProperty("IsOfflineProtectable", s.IsOfflineProtectable));
                        structureJson.Add(new JProperty("DamageLevel", s.DamageLevel));
                        structureJson.Add(new JProperty("BlockCount", s.BlockCount));
                        structureJson.Add(new JProperty("DeviceCount", s.DeviceCount));
                        structureJson.Add(new JProperty("LightCount", s.LightCount));
                        structureJson.Add(new JProperty("TriangleCount", s.TriangleCount));
                        structureJson.Add(new JProperty("Fuel", s.Fuel));
                        structureJson.Add(new JProperty("PowerOutCapacity", s.PowerOutCapacity));
                        structureJson.Add(new JProperty("PowerConsumption", s.PowerConsumption));
                        structureJson.Add(new JProperty("IsShieldActive", s.IsShieldActive));
                        structureJson.Add(new JProperty("ShieldLevel", s.ShieldLevel));
                        structureJson.Add(new JProperty("TotalMass", s.TotalMass));
                        structureJson.Add(new JProperty("HasLandClaimDevice", s.HasLandClaimDevice));
                    }
                    json.Add(new JProperty("Structure", structureJson));
                }
                else
                {
                    json.Add(new JProperty("Structure", null));
                }

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
