// =============================================================================
// NOTE: IPda is NOT accessible from client mods.
//
// IModApi.PDA has [get, set] — unlike GUI, Application, Network and SoundPlayer
// which are getter-only. The game withholds the setter from general client mods,
// granting it only to authorised scenario script mods. This appears intentional:
// IPda can spawn entities, give rewards, and trigger wave attacks — capabilities
// too powerful to expose to arbitrary client mods (same reasoning as IScript).
//
// Consequence: ModApi.PDA is always null for ESB. Every handler returns X with
// "PDA interface is not available in this game context". The implementation is
// correct and complete — if ESB is ever restructured as a scenario script host
// the handlers will work without modification.
//
// Do not remove this file or the integration tests. Keep them as a reference
// implementation and a record of the investigation.
// =============================================================================

using Eleon.Modding;
using Eleon.Pda;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ESB.TopicHandlers.V2
{
    public class Pda
    {
        private readonly ContextData _ctx;

        public Pda(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V2.Pda.ShowMessage",           ShowMessage);
            _ctx.Messenger.RegisterHandler("V2.Pda.ShowDialog",            ShowDialog);
            _ctx.Messenger.RegisterHandler("V2.Pda.GiveReward",            GiveReward);
            _ctx.Messenger.RegisterHandler("V2.Pda.SpawnDropBox",          SpawnDropBox);
            _ctx.Messenger.RegisterHandler("V2.Pda.SetMapMarker",          SetMapMarker);
            _ctx.Messenger.RegisterHandler("V2.Pda.GetPoiLocation",        GetPoiLocation);
            _ctx.Messenger.RegisterHandler("V2.Pda.GetPoiEntityId",        GetPoiEntityId);
            _ctx.Messenger.RegisterHandler("V2.Pda.GetBlockLocation",      GetBlockLocation);
            _ctx.Messenger.RegisterHandler("V2.Pda.GetBlockName",          GetBlockName);
            _ctx.Messenger.RegisterHandler("V2.Pda.SpawnPrefabAtBlock",    SpawnPrefabAtBlock);
            _ctx.Messenger.RegisterHandler("V2.Pda.SpawnPrefabAtPosition", SpawnPrefabAtPosition);
            _ctx.Messenger.RegisterHandler("V2.Pda.SpawnEntityAtPosition", SpawnEntityAtPosition);
            _ctx.Messenger.RegisterHandler("V2.Pda.CreateWaveAttack",      CreateWaveAttack);
            _ctx.Messenger.RegisterHandler("V2.Pda.CreateId",              CreateId);
            _ctx.Messenger.RegisterHandler("V2.Pda.Activate",              Activate);
        }

        // IPda is [get, set] on IModApi — the game only populates it on the IModApi
        // instance given to the mod that owns the PDA scenario script. General client
        // mods like ESB receive a separate IModApi instance where PDA is always null,
        // even when a scenario is actively running. Guard every handler so callers
        // get a clean X rather than a NullReferenceException.
        private async Task<bool> RequirePda(string topic)
        {
            if (_ctx.ModApi.PDA != null) return true;
            await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ErrorJson("PDA interface is not available in this game context"));
            return false;
        }

        // RewardData — Eleon.Pda.RewardType enum: Item, XP, UP, LevelIncrease,
        //              LevelTarget, ReputationTarget, Reputation, DropBox
        private static RewardData ParseReward(JToken t)
        {
            var reward = new RewardData();
            reward.Type      = (RewardType)Enum.Parse(typeof(RewardType), t["Type"].Value<string>());
            reward.Item      = t["Item"]?.Value<string>()          ?? string.Empty;
            reward.Count     = t["Count"]?.Value<int>()            ?? 0;
            reward.Meta      = t["Meta"]?.Value<int>()             ?? 0;
            reward.Faction   = t["Faction"]?.ToObject<string[]>()  ?? new string[0];
            reward.DropBox   = t["DropBox"]?.ToObject<string[]>()  ?? new string[0];
            reward.DropRange = t["DropRange"]?.Value<int>()        ?? 0;
            reward.AirDrop   = t["AirDrop"]?.Value<bool>()         ?? false;
            return reward;
        }

        // -----------------------------------------------------------------------
        // V2.Pda.ShowMessage — displays a message at the bottom-centre of the HUD
        // Payload: { "Message": "...", "Duration"?: 5.0, "HasPrio"?: false,
        //            "CleanupFirst"?: false, "PlayerId"?: -1 }
        // -----------------------------------------------------------------------
        public async Task ShowMessage(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args        = JObject.Parse(payload);
                string message  = args["Message"].Value<string>();
                float  duration = args["Duration"]?.Value<float>()     ?? 5f;
                bool   hasPrio  = args["HasPrio"]?.Value<bool>()       ?? false;
                bool   cleanup  = args["CleanupFirst"]?.Value<bool>()  ?? false;
                int    playerId = args["PlayerId"]?.Value<int>()       ?? -1;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.ShowPdaMessage(message, duration, hasPrio, cleanup, playerId);
                    var json = new JObject(new JProperty("Message", message));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.ShowDialog — shows an interactive PDA dialog with a preset button set
        // Payload: { "Message": "...", "Buttons": "Ok_Cancel" }
        // Buttons values: Ok, Cancel, Quit, Ok_Cancel, Set_Cancel, Yes_No, Skip_LetsGo, LetsGo
        // -----------------------------------------------------------------------
        public async Task ShowDialog(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args       = JObject.Parse(payload);
                string message = args["Message"].Value<string>();
                var buttons    = (ModApiDialogButtons)Enum.Parse(typeof(ModApiDialogButtons), args["Buttons"].Value<string>());

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.ShowPdaDialog(message, buttons);
                    var json = new JObject(
                        new JProperty("Message", message),
                        new JProperty("Buttons", buttons.ToString()));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.GiveReward — gives a reward to the specified player
        // Payload: { "PlayerId": n, "Reward": { "Type": "Item", "Item": "...", "Count": n, ... } }
        // -----------------------------------------------------------------------
        public async Task GiveReward(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args     = JObject.Parse(payload);
                int playerId = args["PlayerId"].Value<int>();
                var reward   = ParseReward(args["Reward"]);

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.GiveReward(reward, playerId);
                    var json = new JObject(
                        new JProperty("PlayerId", playerId),
                        new JProperty("Type",     reward.Type.ToString()));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.SpawnDropBox — spawns a drop container at the specified world position
        // Payload: { "Reward": {...}, "DropPosition": {"X":f,"Y":f,"Z":f}, "DropHeight"?: 20 }
        // -----------------------------------------------------------------------
        public async Task SpawnDropBox(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args         = JObject.Parse(payload);
                var reward       = ParseReward(args["Reward"]);
                var dropPosition = MessageHelpers.ParseVec3(args["DropPosition"]);
                int dropHeight   = args["DropHeight"]?.Value<int>() ?? 20;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.SpawnDropBox(reward, dropPosition, dropHeight);
                    var json = new JObject(
                        new JProperty("DropPosition", MessageHelpers.Vec(dropPosition)),
                        new JProperty("DropHeight",   dropHeight));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.SetMapMarker — creates or removes a temporary map marker for a player
        // Payload: { "Activate": true, "Position": {"X":f,"Y":f,"Z":f},
        //            "MarkerName": "...", "Distance": n, "PlayerId"?: -1 }
        // -----------------------------------------------------------------------
        public async Task SetMapMarker(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args      = JObject.Parse(payload);
                bool activate = args["Activate"].Value<bool>();
                var position  = MessageHelpers.ParseVec3(args["Position"]);
                string name   = args["MarkerName"].Value<string>();
                int distance  = args["Distance"].Value<int>();
                int playerId  = args["PlayerId"]?.Value<int>() ?? -1;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.SetMapMarker(activate, position, name, distance, playerId);
                    var json = new JObject(
                        new JProperty("MarkerName", name),
                        new JProperty("Activate",   activate));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.GetPoiLocation — world position of a named POI
        // Payload: { "PoiName": "..." }
        // Returns Vector3.zero if the POI is not found
        // -----------------------------------------------------------------------
        public async Task GetPoiLocation(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args    = JObject.Parse(payload);
                string name = args["PoiName"].Value<string>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var position = _ctx.ModApi.PDA.GetPoiLocation(name);
                    var json = new JObject(
                        new JProperty("PoiName",  name),
                        new JProperty("Position", MessageHelpers.Vec(position)));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.GetPoiEntityId — entity ID of a named POI
        // Payload: { "PoiName": "..." }
        // Returns 0 if the POI is not found
        // -----------------------------------------------------------------------
        public async Task GetPoiEntityId(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args    = JObject.Parse(payload);
                string name = args["PoiName"].Value<string>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    int entityId = _ctx.ModApi.PDA.GetPoiEntityId(name);
                    var json = new JObject(
                        new JProperty("PoiName",  name),
                        new JProperty("EntityId", entityId));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.GetBlockLocation — local and world positions of a named block on an entity
        // Payload: { "EntityId": n, "BlockName": "..." }
        // Returns Vector3.zero for both positions if the block is not found
        // -----------------------------------------------------------------------
        public async Task GetBlockLocation(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                string name  = args["BlockName"].Value<string>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var localPos = _ctx.ModApi.PDA.GetBlockLocation(entityId, name, out Vector3 worldPos);
                    var json = new JObject(
                        new JProperty("EntityId",  entityId),
                        new JProperty("BlockName", name),
                        new JProperty("LocalPos",  MessageHelpers.Vec(localPos)),
                        new JProperty("WorldPos",  MessageHelpers.Vec(worldPos)));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.GetBlockName — human-readable name for a block type value
        // Payload: { "BlockVal": n }
        // -----------------------------------------------------------------------
        public async Task GetBlockName(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args     = JObject.Parse(payload);
                int blockVal = args["BlockVal"].Value<int>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    string blockName = _ctx.ModApi.PDA.GetBlockName(blockVal);
                    var json = new JObject(
                        new JProperty("BlockVal", blockVal),
                        new JProperty("Name",     blockName ?? string.Empty));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.SpawnPrefabAtBlock — spawn a prefab at the location of a named block
        // Payload: { "PoiName": "...", "BlockName": "...", "PrefabName": "...", "Height": f }
        // -----------------------------------------------------------------------
        public async Task SpawnPrefabAtBlock(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args         = JObject.Parse(payload);
                string poiName   = args["PoiName"].Value<string>();
                string blockName = args["BlockName"].Value<string>();
                string prefab    = args["PrefabName"].Value<string>();
                float  height    = args["Height"].Value<float>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    int entityId = _ctx.ModApi.PDA.SpawnPrefabAtBlock(poiName, blockName, prefab, height);
                    var json = new JObject(
                        new JProperty("EntityId",   entityId),
                        new JProperty("PrefabName", prefab));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.SpawnPrefabAtPosition — spawn a prefab at a world position
        // Payload: { "PrefabName": "...", "Position": {"X":f,"Y":f,"Z":f} }
        // -----------------------------------------------------------------------
        public async Task SpawnPrefabAtPosition(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args      = JObject.Parse(payload);
                string prefab = args["PrefabName"].Value<string>();
                var position  = MessageHelpers.ParseVec3(args["Position"]);

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    int entityId = _ctx.ModApi.PDA.SpawnPrefabAtPosition(prefab, position);
                    var json = new JObject(
                        new JProperty("EntityId",   entityId),
                        new JProperty("PrefabName", prefab));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.SpawnEntityAtPosition — spawn an entity at a world position
        // Payload: { "Position": {"X":f,"Y":f,"Z":f}, "ClassName": "...", "Faction": "...",
        //            "Height"?: 0, "BTerrain"?: true, "AttachToEntity"?: -1 }
        // -----------------------------------------------------------------------
        public async Task SpawnEntityAtPosition(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args         = JObject.Parse(payload);
                var position     = MessageHelpers.ParseVec3(args["Position"]);
                string className = args["ClassName"].Value<string>();
                string faction   = args["Faction"].Value<string>();
                int    height    = args["Height"]?.Value<int>()         ?? 0;
                bool   bTerrain  = args["BTerrain"]?.Value<bool>()      ?? true;
                int    attachTo  = args["AttachToEntity"]?.Value<int>() ?? -1;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    int entityId = _ctx.ModApi.PDA.SpawnEntityAtPosition(position, className, faction, height, bTerrain, attachTo);
                    var json = new JObject(
                        new JProperty("EntityId",  entityId),
                        new JProperty("ClassName", className));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.CreateWaveAttack — creates a wave attack from the provided parameters
        // Payload: { "Name": "...", "Cost": n, "Target": "...", "Faction": "..." }
        // -----------------------------------------------------------------------
        public async Task CreateWaveAttack(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args = JObject.Parse(payload);
                var wave = new WaveStartData();
                wave.Name    = args["Name"].Value<string>();
                wave.Cost    = args["Cost"].Value<int>();
                wave.Target  = args["Target"].Value<string>();
                wave.Faction = args["Faction"].Value<string>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    uint waveId = _ctx.ModApi.PDA.CreateWaveAttack(wave);
                    var json = new JObject(
                        new JProperty("WaveId", waveId),
                        new JProperty("Name",   wave.Name));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.CreateId — creates a script-internal integer ID from a title string
        // Payload: { "Title": "..." }
        // -----------------------------------------------------------------------
        public async Task CreateId(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args     = JObject.Parse(payload);
                string title = args["Title"].Value<string>();

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    int id = _ctx.ModApi.PDA.CreateId(title);
                    var json = new JObject(
                        new JProperty("Title", title),
                        new JProperty("Id",    id));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // V2.Pda.Activate — executes the PDA script (TBD per API docs)
        // Payload: { "Reset"?: false }
        // -----------------------------------------------------------------------
        public async Task Activate(string topic, string payload)
        {
            try
            {
                if (!await RequirePda(topic)) return;

                var args   = JObject.Parse(payload);
                bool reset = args["Reset"]?.Value<bool>() ?? false;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    _ctx.ModApi.PDA.Activate(reset);
                    var json = new JObject(new JProperty("Activated", true));
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
