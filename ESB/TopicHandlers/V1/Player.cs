using Eleon.Modding;
using ESB.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public class Player
    {
        private readonly ContextData _ctx;

        public Player(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("V1.Player.GetInventory",          GetInventory);
            _ctx.Messenger.RegisterHandler("V1.Player.GetInfo",               GetInfo);
            _ctx.Messenger.RegisterHandler("V1.Player.List",                  List);
            _ctx.Messenger.RegisterHandler("V1.Player.GetAndRemoveInventory", GetAndRemoveInventory);
            _ctx.Messenger.RegisterHandler("V1.Player.SetInventory",          SetInventory);
            _ctx.Messenger.RegisterHandler("V1.Player.AddItem",               AddItem);
            _ctx.Messenger.RegisterHandler("V1.Player.ItemExchange",          ItemExchange);
            _ctx.Messenger.RegisterHandler("V1.Player.GetCredits",            GetCredits);
            _ctx.Messenger.RegisterHandler("V1.Player.SetCredits",            SetCredits);
            _ctx.Messenger.RegisterHandler("V1.Player.AddCredits",            AddCredits);
            _ctx.Messenger.RegisterHandler("V1.Player.SetInfo",               SetInfo);
            _ctx.Messenger.RegisterHandler("V1.Player.ChangePlayfield",       ChangePlayfield);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static ItemStack[] ParseItemStacks(JArray arr)
        {
            if (arr == null) return Array.Empty<ItemStack>();
            return arr.Select(t => new ItemStack(t["id"].Value<int>(), t["count"].Value<int>())
            {
                slotIdx = (byte)(t["slotIdx"]?.Value<int>() ?? 0),
                ammo    = t["ammo"]?.Value<int>()   ?? 0,
                decay   = t["decay"]?.Value<int>()  ?? 0,
            }).ToArray();
        }

        private static PVector3 ParsePVec(JToken t) =>
            t != null
                ? new PVector3(t["x"].Value<float>(), t["y"].Value<float>(), t["z"].Value<float>())
                : new PVector3(0f, 0f, 0f);

        // -------------------------------------------------------------------------
        // V1.Player.GetInventory — read full inventory for any player by EntityId
        // -------------------------------------------------------------------------
        public async Task GetInventory(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var inv = await _ctx.ModBase.Broker.Request_Player_GetInventory(new Id(entityId));

                var json = new JObject(new JProperty("Data",
                    inv != null ? JObject.FromObject(inv) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.GetInfo — read full PlayerInfo for any player by EntityId
        // -------------------------------------------------------------------------
        public async Task GetInfo(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                // The game silently drops Request_Player_Info for non-player entity IDs
                // (structures, vessels, etc.) -- Event_Player_Info never fires and the task
                // hangs indefinitely. Guard with a timeout and return X so callers get a
                // clean error instead of a silent hang.
                var infoTask = _ctx.ModBase.Broker.Request_Player_Info(new Id(entityId));
                if (await Task.WhenAny(infoTask, Task.Delay(3000)) != infoTask)
                {
                    await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                        MessageHelpers.ErrorJson($"No response for entity {entityId} — not a connected player entity ID"));
                    return;
                }

                var info = await infoTask;
                var json = new JObject(new JProperty("Data",
                    info != null ? JObject.FromObject(info) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.List — list EntityIds of all connected players
        // Payload: {} (no parameters)
        // Response: {"Data": [entityId, ...]}
        // -------------------------------------------------------------------------
        public async Task List(string topic, string payload)
        {
            try
            {
                var result = await _ctx.ModBase.Broker.Request_Player_List();

                var json = new JObject(new JProperty("Data",
                    result?.list != null ? JArray.FromObject(result.list) : new JArray()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.GetCredits — read credits for any player by EntityId
        // -------------------------------------------------------------------------
        public async Task GetCredits(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var result = await _ctx.ModBase.Broker.Request_Player_Credits(new Id(entityId));

                var json = new JObject(new JProperty("Data", JObject.FromObject(result)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.SetCredits — overwrite credits for a player
        // Payload: {"EntityId": int, "Credits": double}
        // Response: {"Data": {"id": int, "credits": double}} (confirmed state)
        // -------------------------------------------------------------------------
        public async Task SetCredits(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));
                var credits  = args["Credits"].Value<double>();

                await _ctx.ModBase.Broker.Request_Player_SetCredits(new IdCredits(entityId, credits));

                var result = await _ctx.ModBase.Broker.Request_Player_Credits(new Id(entityId));
                var json = new JObject(new JProperty("Data", JObject.FromObject(result)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.AddCredits — add (positive) or subtract (negative) credits
        // Payload: {"EntityId": int, "Credits": double}
        // Response: {"Data": {"id": int, "credits": double}} (new balance)
        // -------------------------------------------------------------------------
        public async Task AddCredits(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));
                var amount   = args["Credits"].Value<double>();

                await _ctx.ModBase.Broker.Request_Player_AddCredits(new IdCredits(entityId, amount));

                var result = await _ctx.ModBase.Broker.Request_Player_Credits(new Id(entityId));
                var json = new JObject(new JProperty("Data", JObject.FromObject(result)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.GetAndRemoveInventory — atomically read and clear player inventory
        // ⚠ Destructive: clears the player's toolbar and bag.
        // -------------------------------------------------------------------------
        public async Task GetAndRemoveInventory(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));

                var inv = await _ctx.ModBase.Broker.Request_Player_GetAndRemoveInventory(new Id(entityId));

                var json = new JObject(new JProperty("Data",
                    inv != null ? JObject.FromObject(inv) : (JToken)JValue.CreateNull()));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.SetInventory — overwrite player toolbar and bag
        // Payload: {"PlayerId": int, "Toolbelt": [...], "Bag": [...]}
        //   Item: {"id": int, "count": int, "slotIdx": int, "ammo": int, "decay": int}
        // Response: {"Data": {inventory}} (confirmed state)
        // ⚠ Destructive: replaces the player's entire inventory.
        // -------------------------------------------------------------------------
        public async Task SetInventory(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var playerId = Convert.ToInt32(args.GetValue("PlayerId"));
                var toolbelt = ParseItemStacks(args["Toolbelt"] as JArray);
                var bag      = ParseItemStacks(args["Bag"]      as JArray);

                var result = await _ctx.ModBase.Broker.Request_Player_SetInventory(new Inventory(playerId, toolbelt, bag));

                var json = new JObject(new JProperty("Data", JObject.FromObject(result)));
                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.AddItem — add a single item stack to a player's inventory
        // Payload: {"EntityId": int, "ItemId": int, "Count": int}
        // Response: {"Ok": true}
        // -------------------------------------------------------------------------
        public async Task AddItem(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                var entityId = Convert.ToInt32(args.GetValue("EntityId"));
                var itemId   = args["ItemId"].Value<int>();
                var count    = args["Count"].Value<int>();

                await _ctx.ModBase.Broker.Request_Player_AddItem(new IdItemStack(entityId, new ItemStack(itemId, count)));

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.ItemExchange — show an item-exchange dialog to a player and wait
        // Payload: {"EntityId": int, "Title": string, "Desc": string,
        //           "ButtonText": string, "Items": [...]}
        // Response: {"Data": {ItemExchangeInfo}} — contains items the player submitted
        // -------------------------------------------------------------------------
        public async Task ItemExchange(string topic, string payload)
        {
            try
            {
                var args       = JObject.Parse(payload);
                var entityId   = Convert.ToInt32(args.GetValue("EntityId"));
                var title      = args["Title"]?.Value<string>()      ?? "";
                var desc       = args["Desc"]?.Value<string>()       ?? "";
                var buttonText = args["ButtonText"]?.Value<string>() ?? "OK";
                var items      = ParseItemStacks(args["Items"] as JArray);

                var result = await _ctx.ModBase.Broker.Request_Player_ItemExchange(
                    new ItemExchangeInfo(entityId, title, desc, buttonText, items));

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
        // V1.Player.SetInfo — patch player stats via PlayerInfoSet (null = unchanged)
        // Payload: {"EntityId": int, "Health": int?, "Food": int?, ...}
        // Response: {"Ok": true}
        // -------------------------------------------------------------------------
        public async Task SetInfo(string topic, string payload)
        {
            try
            {
                var args = JObject.Parse(payload);
                var infoSet = new PlayerInfoSet
                {
                    entityId         = Convert.ToInt32(args.GetValue("EntityId")),
                    startPlayfield   = args["StartPlayfield"]?.Value<string>(),
                    health           = args["Health"]?.Value<int?>(),
                    healthMax        = args["HealthMax"]?.Value<int?>(),
                    food             = args["Food"]?.Value<int?>(),
                    foodMax          = args["FoodMax"]?.Value<int?>(),
                    oxygen           = args["Oxygen"]?.Value<int?>(),
                    oxygenMax        = args["OxygenMax"]?.Value<int?>(),
                    stamina          = args["Stamina"]?.Value<int?>(),
                    staminaMax       = args["StaminaMax"]?.Value<int?>(),
                    radiation        = args["Radiation"]?.Value<int?>(),
                    radiationMax     = args["RadiationMax"]?.Value<int?>(),
                    bodyTemp         = args["BodyTemp"]?.Value<int?>(),
                    bodyTempMax      = args["BodyTempMax"]?.Value<int?>(),
                    experiencePoints = args["ExperiencePoints"]?.Value<int?>(),
                    upgradePoints    = args["UpgradePoints"]?.Value<int?>(),
                    origin           = args["Origin"]?.Value<int?>(),
                    factionGroup     = args["FactionGroup"] != null ? (byte?)args["FactionGroup"].Value<int>() : null,
                    factionId        = args["FactionId"]?.Value<int?>(),
                    factionRole      = args["FactionRole"] != null ? (byte?)args["FactionRole"].Value<int>() : null,
                };

                await _ctx.ModBase.Broker.Request_Player_SetPlayerInfo(infoSet);

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -------------------------------------------------------------------------
        // V1.Player.ChangePlayfield — move a player to a different playfield
        // Payload: {"EntityId": int, "Playfield": string,
        //           "Pos": {"x":f,"y":f,"z":f}, "Rot": {"x":f,"y":f,"z":f}}
        // Response: {"Ok": true}
        // ⚠ Disruptive: teleports the player to another playfield immediately.
        // -------------------------------------------------------------------------
        public async Task ChangePlayfield(string topic, string payload)
        {
            try
            {
                var args      = JObject.Parse(payload);
                var entityId  = Convert.ToInt32(args.GetValue("EntityId"));
                var playfield = args["Playfield"].Value<string>();
                var pos       = ParsePVec(args["Pos"]);
                var rot       = ParsePVec(args["Rot"]);

                await _ctx.ModBase.Broker.Request_Player_ChangePlayerfield(
                    new IdPlayfieldPositionRotation(entityId, playfield, pos, rot));

                await _ctx.Messenger.SendAsync(MessageClass.Response, topic, "{\"Ok\":true}");
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }
    }
}
