using Eleon.Modding;
using ESB.Models;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Handler design: "V2.Player" returns player properties (all or selected via "Properties" key).
// "V2.Player.<Method>" invokes a method. See Docs/ApiLayers.md for the pattern rationale.

namespace ESB.TopicHandlers.V2
{
    // -------------------------------------------------------------------------
    // [Flags] enum — one bit per IPlayer / IEntity property.
    // Encoding the caller's "Properties" list into a mask allows a single array
    // pass to build the response JSON with no per-request heap allocation for
    // the dispatch mechanism itself.
    // -------------------------------------------------------------------------
    [Flags]
    enum PlayerProp : long
    {
        Id                = 1L << 0,
        Name              = 1L << 1,
        SteamId           = 1L << 2,
        SteamOwnerId      = 1L << 3,
        StartPlayfield    = 1L << 4,
        Origin            = 1L << 5,
        Permission        = 1L << 6,
        Health            = 1L << 7,
        HealthMax         = 1L << 8,
        Oxygen            = 1L << 9,
        OxygenMax         = 1L << 10,
        Stamina           = 1L << 11,
        StaminaMax        = 1L << 12,
        Food              = 1L << 13,
        FoodMax           = 1L << 14,
        Radiation         = 1L << 15,
        RadiationMax      = 1L << 16,
        BodyTemp          = 1L << 17,
        BodyTempMax       = 1L << 18,
        Credits           = 1L << 19,
        ExperiencePoints  = 1L << 20,
        UpgradePoints     = 1L << 21,
        Kills             = 1L << 22,
        Died              = 1L << 23,
        Ping              = 1L << 24,
        HomeBaseId        = 1L << 25,
        IsPilot           = 1L << 26,
        IsLocal           = 1L << 27,
        IsProxy           = 1L << 28,
        IsPoi             = 1L << 29,
        BelongsTo         = 1L << 30,
        DockedTo          = 1L << 31,
        Type              = 1L << 32,
        FactionData       = 1L << 33,
        Faction           = 1L << 34,
        FactionRole       = 1L << 35,
        CurrentStructureId = 1L << 36,
        DrivingEntityId   = 1L << 37,
        Position          = 1L << 38,
        Forward           = 1L << 39,
        Rotation          = 1L << 40,
        Toolbar           = 1L << 41,
        Bag               = 1L << 42,
        All               = ~0L
    }

    // Value type — the array is a single contiguous block allocated at class load.
    struct PropDef
    {
        public string Name;
        public PlayerProp Flag;
        public Func<IPlayer, JToken> Get;
    }

    public class Player
    {
        private readonly ContextData _ctx;

        public Player(ContextData ctx) { _ctx = ctx; }

        // -------------------------------------------------------------------------
        // Static property definitions — one entry per IPlayer / IEntity property.
        // Getters are non-capturing lambdas; the compiler caches each as a single
        // delegate instance for the lifetime of the class.
        // -------------------------------------------------------------------------
        static JToken Vec3(Vector3 v)    => new JObject(new JProperty("X", v.x), new JProperty("Y", v.y), new JProperty("Z", v.z));
        static JToken Vec4(Quaternion q) => new JObject(new JProperty("X", q.x), new JProperty("Y", q.y), new JProperty("Z", q.z), new JProperty("W", q.w));
        static JToken FD(FactionData fd) => new JObject(new JProperty("Group", fd.Group.ToString()), new JProperty("Id", fd.Id));

        static readonly PropDef[] Props = new PropDef[]
        {
            new PropDef { Name = "Id",               Flag = PlayerProp.Id,               Get = p => JToken.FromObject(p.Id) },
            new PropDef { Name = "Name",             Flag = PlayerProp.Name,             Get = p => JToken.FromObject(p.Name) },
            new PropDef { Name = "SteamId",          Flag = PlayerProp.SteamId,          Get = p => JToken.FromObject(p.SteamId) },
            new PropDef { Name = "SteamOwnerId",     Flag = PlayerProp.SteamOwnerId,     Get = p => JToken.FromObject(p.SteamOwnerId) },
            new PropDef { Name = "StartPlayfield",   Flag = PlayerProp.StartPlayfield,   Get = p => JToken.FromObject(p.StartPlayfield) },
            new PropDef { Name = "Origin",           Flag = PlayerProp.Origin,           Get = p => JToken.FromObject(p.Origin) },
            new PropDef { Name = "Permission",       Flag = PlayerProp.Permission,       Get = p => JToken.FromObject(p.Permission) },
            new PropDef { Name = "Health",           Flag = PlayerProp.Health,           Get = p => JToken.FromObject(p.Health) },
            new PropDef { Name = "HealthMax",        Flag = PlayerProp.HealthMax,        Get = p => JToken.FromObject(p.HealthMax) },
            new PropDef { Name = "Oxygen",           Flag = PlayerProp.Oxygen,           Get = p => JToken.FromObject(p.Oxygen) },
            new PropDef { Name = "OxygenMax",        Flag = PlayerProp.OxygenMax,        Get = p => JToken.FromObject(p.OxygenMax) },
            new PropDef { Name = "Stamina",          Flag = PlayerProp.Stamina,          Get = p => JToken.FromObject(p.Stamina) },
            new PropDef { Name = "StaminaMax",       Flag = PlayerProp.StaminaMax,       Get = p => JToken.FromObject(p.StaminaMax) },
            new PropDef { Name = "Food",             Flag = PlayerProp.Food,             Get = p => JToken.FromObject(p.Food) },
            new PropDef { Name = "FoodMax",          Flag = PlayerProp.FoodMax,          Get = p => JToken.FromObject(p.FoodMax) },
            new PropDef { Name = "Radiation",        Flag = PlayerProp.Radiation,        Get = p => JToken.FromObject(p.Radiation) },
            new PropDef { Name = "RadiationMax",     Flag = PlayerProp.RadiationMax,     Get = p => JToken.FromObject(p.RadiationMax) },
            new PropDef { Name = "BodyTemp",         Flag = PlayerProp.BodyTemp,         Get = p => JToken.FromObject(p.BodyTemp) },
            new PropDef { Name = "BodyTempMax",      Flag = PlayerProp.BodyTempMax,      Get = p => JToken.FromObject(p.BodyTempMax) },
            new PropDef { Name = "Credits",          Flag = PlayerProp.Credits,          Get = p => JToken.FromObject(p.Credits) },
            new PropDef { Name = "ExperiencePoints", Flag = PlayerProp.ExperiencePoints, Get = p => JToken.FromObject(p.ExperiencePoints) },
            new PropDef { Name = "UpgradePoints",    Flag = PlayerProp.UpgradePoints,    Get = p => JToken.FromObject(p.UpgradePoints) },
            new PropDef { Name = "Kills",            Flag = PlayerProp.Kills,            Get = p => JToken.FromObject(p.Kills) },
            new PropDef { Name = "Died",             Flag = PlayerProp.Died,             Get = p => JToken.FromObject(p.Died) },
            new PropDef { Name = "Ping",             Flag = PlayerProp.Ping,             Get = p => JToken.FromObject(p.Ping) },
            new PropDef { Name = "HomeBaseId",       Flag = PlayerProp.HomeBaseId,       Get = p => JToken.FromObject(p.HomeBaseId) },
            new PropDef { Name = "IsPilot",          Flag = PlayerProp.IsPilot,          Get = p => JToken.FromObject(p.IsPilot) },
            new PropDef { Name = "IsLocal",          Flag = PlayerProp.IsLocal,          Get = p => JToken.FromObject(p.IsLocal) },
            new PropDef { Name = "IsProxy",          Flag = PlayerProp.IsProxy,          Get = p => JToken.FromObject(p.IsProxy) },
            new PropDef { Name = "IsPoi",            Flag = PlayerProp.IsPoi,            Get = p => JToken.FromObject(p.IsPoi) },
            new PropDef { Name = "BelongsTo",        Flag = PlayerProp.BelongsTo,        Get = p => JToken.FromObject(p.BelongsTo) },
            new PropDef { Name = "DockedTo",         Flag = PlayerProp.DockedTo,         Get = p => JToken.FromObject(p.DockedTo) },
            new PropDef { Name = "Type",             Flag = PlayerProp.Type,             Get = p => JToken.FromObject(p.Type.ToString()) },
            new PropDef { Name = "FactionData",      Flag = PlayerProp.FactionData,      Get = p => FD(p.FactionData) },
            new PropDef { Name = "Faction",          Flag = PlayerProp.Faction,          Get = p => FD(p.Faction) },
            new PropDef { Name = "FactionRole",      Flag = PlayerProp.FactionRole,      Get = p => JToken.FromObject(p.FactionRole.ToString()) },
            new PropDef { Name = "CurrentStructureId", Flag = PlayerProp.CurrentStructureId, Get = p => p.CurrentStructure != null ? JToken.FromObject(p.CurrentStructure.Id) : JValue.CreateNull() },
            new PropDef { Name = "DrivingEntityId",  Flag = PlayerProp.DrivingEntityId,  Get = p => p.DrivingEntity  != null ? JToken.FromObject(p.DrivingEntity.Id)   : JValue.CreateNull() },
            new PropDef { Name = "Position",         Flag = PlayerProp.Position,         Get = p => Vec3(p.Position) },
            new PropDef { Name = "Forward",          Flag = PlayerProp.Forward,          Get = p => Vec3(p.Forward) },
            new PropDef { Name = "Rotation",         Flag = PlayerProp.Rotation,         Get = p => Vec4(p.Rotation) },
            new PropDef { Name = "Toolbar",          Flag = PlayerProp.Toolbar,          Get = p => SerializeItemStacks(p.Toolbar) },
            new PropDef { Name = "Bag",              Flag = PlayerProp.Bag,              Get = p => SerializeItemStacks(p.Bag) },
        };

        // Materialized once at class load — returned in X-responses for invalid property names.
        static readonly string[] ValidPropertyNames = Props.Select(d => d.Name).ToArray();

        static JToken SerializeItemStacks(List<ItemStack> stacks)
        {
            var arr = new JArray();
            if (stacks == null) return arr;
            foreach (var s in stacks)
                arr.Add(new JObject(
                    new JProperty("id",      s.id),
                    new JProperty("count",   s.count),
                    new JProperty("slotIdx", s.slotIdx),
                    new JProperty("ammo",    s.ammo),
                    new JProperty("decay",   s.decay)));
            return arr;
        }

        // Encode a list of requested property names into a PlayerProp mask.
        // Returns true on success; populates invalidNames if any name is unrecognised.
        static bool TryParseMask(JArray requested, out PlayerProp mask, out List<string> invalidNames)
        {
            mask = 0;
            invalidNames = null;
            foreach (var token in requested)
            {
                var name = token.Value<string>();
                bool found = false;
                foreach (var def in Props)
                {
                    if (def.Name == name) { mask |= def.Flag; found = true; break; }
                }
                if (!found)
                    (invalidNames ?? (invalidNames = new List<string>())).Add(name);
            }
            return invalidNames == null;
        }

        // -------------------------------------------------------------------------
        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Player",              Properties);
            _ctx.Messenger.RegisterHandler("V2.Player.Teleport",     Teleport);
            _ctx.Messenger.RegisterHandler("V2.Player.DamageEntity", DamageEntity);
        }

        // -------------------------------------------------------------------------
        // V2.Player — property getter
        //   payload (all optional):
        //     "Properties": ["SteamId", "Health", ...]  — omit for all properties
        //     "EntityId": int  — reserved for future V1 path (returns X for now)
        // -------------------------------------------------------------------------
        public async Task Properties(string topic, string payload)
        {
            try
            {
                JObject args = string.IsNullOrWhiteSpace(payload) ? null : JObject.Parse(payload);

                // EntityId: future V1 path — not implemented in this pass
                if (args?["EntityId"] != null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("EntityId lookup requires DedicatedServer"));
                    return;
                }

                // Parse optional "Properties" filter
                PlayerProp mask = PlayerProp.All;
                var propArray = args?["Properties"] as JArray;
                if (propArray != null)
                {
                    if (!TryParseMask(propArray, out mask, out var invalid))
                    {
                        var errObj = new JObject(
                            new JProperty("Error",            "InvalidProperty"),
                            new JProperty("InvalidProperties", new JArray(invalid)),
                            new JProperty("ValidProperties",   new JArray(ValidPropertyNames)));
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            errObj.ToString(Formatting.None));
                        return;
                    }
                }

                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    json = new JObject();
                    foreach (var def in Props)
                    {
                        if ((mask & def.Flag) != 0)
                        {
                            try   { json[def.Name] = def.Get(localPlayer); }
                            catch { json[def.Name] = JValue.CreateNull(); }
                        }
                    }
                    await Task.CompletedTask;
                });

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                    json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Player.Teleport
        //   payload: { "Pos": {X,Y,Z} }
        //         or { "Pos": {X,Y,Z}, "Playfield": "name", "Rot": {X,Y,Z} }
        // -------------------------------------------------------------------------
        public async Task Teleport(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                JObject args = JObject.Parse(payload);
                string playfield = args["Playfield"]?.ToString();

                if (args["Pos"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("Pos argument is required"));
                    return;
                }

                if (playfield != null && args["Rot"] == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("Rot argument is required when Playfield is provided"));
                    return;
                }

                Vector3 pos = MessageHelpers.ParseVec3(args["Pos"]);
 
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    bool result;
                    if (playfield == null)
                        result = localPlayer.Teleport(pos);
                    else
                    {
                        Vector3 rot = MessageHelpers.ParseVec3(args["Rot"]);
                        result = localPlayer.Teleport(playfield, pos, rot);
                    }
                    var json = new JObject(new JProperty("Teleport", result));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        json.ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V2.Player.DamageEntity
        //   payload: { "DamageAmount": int, "DamageType": int }
        //   Use DamageAmount:0 for a safe no-op call in tests.
        // -------------------------------------------------------------------------
        public async Task DamageEntity(string topic, string payload)
        {
            try
            {
                var localPlayer = _ctx.ModApi.Application.LocalPlayer;
                if (localPlayer == null)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson("LocalPlayer is null — no active player in this game mode"));
                    return;
                }

                JObject args = JObject.Parse(payload);
                int damageAmount = args["DamageAmount"]?.Value<int>() ?? 0;
                int damageType   = args["DamageType"]?.Value<int>()   ?? 0;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    localPlayer.DamageEntity(damageAmount, damageType);
                    var json = new JObject(new JProperty("DamageEntity", true));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic,
                        json.ToString(Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
