using Eleon.Modding;
using ESB.Models;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Entity
    {
        private readonly ContextData _ctx;

        public Entity(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Entity.GetPosAndRot",    GetPosAndRot);
            _ctx.Messenger.RegisterHandler("V1.Entity.Teleport",        Teleport);
            _ctx.Messenger.RegisterHandler("V1.Entity.ChangePlayfield", ChangePlayfield);
            _ctx.Messenger.RegisterHandler("V1.Entity.Destroy",         Destroy);
            _ctx.Messenger.RegisterHandler("V1.Entity.Spawn",           Spawn);
            _ctx.Messenger.RegisterHandler("V1.Entity.SetName",         SetName);
            _ctx.Messenger.RegisterHandler("V1.Entity.Export",          Export);
            _ctx.Messenger.RegisterHandler("V1.Entity.NewId",           NewId);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static PVector3 ParsePVec(JToken t) =>
            t != null
                ? new PVector3(t["x"].Value<float>(), t["y"].Value<float>(), t["z"].Value<float>())
                : new PVector3(0f, 0f, 0f);

        // -------------------------------------------------------------------------
        // V1.Entity.GetPosAndRot -- read position and rotation of any entity by Id
        // Payload: {"EntityId": int}
        // Response: {"Data": {"id": int, "pos": {...}, "rot": {...}}}
        // -------------------------------------------------------------------------
        public async Task GetPosAndRot(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var requestTask = _ctx.ModBase.Request_Entity_PosAndRot(new Id(entityId));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for entity {entityId}"));
                    return;
                }
                var result = await requestTask;

                var json = new JObject(new JProperty("Data",
                    result != null ? JObject.FromObject(result) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.Teleport -- teleport an entity within its current playfield
        // Payload: {"EntityId": int, "Pos": {"x":f,"y":f,"z":f}, "Rot": {"x":f,"y":f,"z":f}}
        // Response: {"Ok": true}
        // Destructive: moves the entity immediately.
        // -------------------------------------------------------------------------
        public async Task Teleport(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));
                var pos      = ParsePVec(args["Pos"]);
                var rot      = ParsePVec(args["Rot"]);

                var requestTask = _ctx.ModBase.Request_Entity_Teleport(
                    new IdPositionRotation(entityId, pos, rot));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Teleport of entity {entityId}"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.ChangePlayfield -- move an entity to a different playfield
        // Payload: {"EntityId": int, "Playfield": string,
        //           "Pos": {"x":f,"y":f,"z":f}, "Rot": {"x":f,"y":f,"z":f}}
        // Response: {"Ok": true}
        // Destructive: transfers the entity to the destination playfield immediately.
        // -------------------------------------------------------------------------
        public async Task ChangePlayfield(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                var entityId  = Convert.ToInt32(args.GetValue("EntityId"));
                var playfield = (string)args["Playfield"];
                var pos       = ParsePVec(args["Pos"]);
                var rot       = ParsePVec(args["Rot"]);

                var requestTask = _ctx.ModBase.Request_Entity_ChangePlayfield(
                    new IdPlayfieldPositionRotation(entityId, playfield, pos, rot));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for ChangePlayfield of entity {entityId}"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.Destroy -- destroy an entity by Id
        // Payload: {"EntityId": int}
        // Response: {"Ok": true}
        // Destructive: permanently removes the entity from the game world.
        // -------------------------------------------------------------------------
        public async Task Destroy(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var requestTask = _ctx.ModBase.Request_Entity_Destroy(new Id(entityId));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for Destroy of entity {entityId}"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.Spawn -- spawn a new entity in the game world
        // Payload: {"Playfield": string, "Pos": {...}, "Rot": {...}, "Name": string,
        //           "Type": int, "EntityTypeName": string?, "PrefabName": string?,
        //           "PrefabDir": string?, "FactionGroup": int?, "FactionId": int?,
        //           "ForceEntityId": int?, "ExportedEntityDat": string?}
        // Response: {"Ok": true}
        // WARNING: verified non-functional on a dedicated MP server. The request
        // returns Event_Ok but the entity is never created -- it does not appear in
        // ListGlobal, GetPosAndRot, or Destroy. ForceEntityId is also silently ignored.
        // Root cause unknown; may be a DediAPI limitation or require playfield pre-load.
        // -------------------------------------------------------------------------
        public async Task Spawn(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var info = new EntitySpawnInfo
                {
                    playfield         = (string)args["Playfield"],
                    pos               = ParsePVec(args["Pos"]),
                    rot               = ParsePVec(args["Rot"]),
                    name              = args["Name"]?.Value<string>()              ?? "",
                    type              = args["Type"] != null
                                            ? (byte)args["Type"].Value<int>()
                                            : (byte)0,
                    entityTypeName    = args["EntityTypeName"]?.Value<string>()    ?? "",
                    prefabName        = args["PrefabName"]?.Value<string>()        ?? "",
                    prefabDir         = args["PrefabDir"]?.Value<string>()         ?? "",
                    factionGroup      = args["FactionGroup"] != null
                                            ? (byte)args["FactionGroup"].Value<int>()
                                            : (byte)0,
                    factionId         = args["FactionId"]?.Value<int>()            ?? 0,
                    forceEntityId     = args["ForceEntityId"]?.Value<int>()        ?? 0,
                    exportedEntityDat = args["ExportedEntityDat"]?.Value<string>(),
                };

                var requestTask = _ctx.ModBase.Request_Entity_Spawn(info);
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for Spawn"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.SetName -- rename an entity
        // Payload: {"EntityId": int, "Playfield": string, "Name": string}
        // Response: {"Ok": true}
        // -------------------------------------------------------------------------
        public async Task SetName(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                var entityId  = Convert.ToInt32(args.GetValue("EntityId"));
                var playfield = (string)args["Playfield"];
                var name      = (string)args["Name"];

                var requestTask = _ctx.ModBase.Request_Entity_SetName(
                    new IdPlayfieldName(entityId, playfield, name));
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response from game for SetName of entity {entityId}"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.Export -- export an entity as a blueprint to a server file path
        // Payload: {"EntityId": int, "Playfield": string, "FilePath": string,
        //           "IsForceUnload": bool?}
        // Response: {"Ok": true}
        // -------------------------------------------------------------------------
        public async Task Export(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var info = new EntityExportInfo
                {
                    id            = Convert.ToInt32(args.GetValue("EntityId")),
                    playfield     = (string)args["Playfield"],
                    filePath      = (string)args["FilePath"],
                    isForceUnload = args["IsForceUnload"]?.Value<bool>() ?? false,
                };

                var requestTask = _ctx.ModBase.Request_Entity_Export(info);
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for Export"));
                    return;
                }
                await requestTask;

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Entity.NewId -- allocate a new entity ID from the server
        // Payload: {} (no parameters)
        // Response: {"Data": {"id": int}}
        // Use this to reserve an entity ID before spawning with V1.Entity.Spawn.
        // -------------------------------------------------------------------------
        public async Task NewId(string topic, string payload)
        {
            try
            {
                var requestTask = _ctx.ModBase.Request_NewEntityId();
                if (await Task.WhenAny(requestTask, Task.Delay(3000)) != requestTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("No response from game for NewEntityId"));
                    return;
                }
                var result = await requestTask;

                var json = new JObject(new JProperty("Data",
                    result != null ? JObject.FromObject(result) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
