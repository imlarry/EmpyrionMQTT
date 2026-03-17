using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public class Container : IContainer
    {
        private readonly ContextData _ctx;

        public Container(ContextData ctx)
        {
            _ctx = ctx;
        }

        public void Register()
        {
            _ctx.Messenger.RegisterHandler("Container.Get",          Get);
            _ctx.Messenger.RegisterHandler("Container.Contains",     Contains);
            _ctx.Messenger.RegisterHandler("Container.GetTotalItems", GetTotalItems);
            _ctx.Messenger.RegisterHandler("Container.AddItems",     AddItems);
            _ctx.Messenger.RegisterHandler("Container.RemoveItems",  RemoveItems);
            _ctx.Messenger.RegisterHandler("Container.Clear",        Clear);
            _ctx.Messenger.RegisterHandler("Container.SetContent",   SetContent);
        }

        // Shared lookup: entity from cache → IStructure, or send exception and return null.
        // GetDevice<T> must be called on the main thread; callers wrap in RunOnMainThread.
        private async Task<Eleon.Modding.IStructure> GetStructure(string topic, int entityId)
        {
            var entity = _ctx.GetEntityByKey(entityId);
            if (entity == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} not found in LoadedEntity cache"));
                return null;
            }
            if (entity.Structure == null)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                    MessageHelpers.ErrorJson($"Entity {entityId} has no Structure (not a structure entity)"));
                return null;
            }
            return entity.Structure;
        }

        // ItemStack ↔ JSON {Id, Count, SlotIdx, Ammo, Decay}
        // ItemStack fields are lowercase in the Eleon API — normalized to PascalCase per ESB convention.
        private static JObject ItemStackJson(ItemStack s) => new JObject(
            new JProperty("Id",      s.id),
            new JProperty("Count",   s.count),
            new JProperty("SlotIdx", s.slotIdx),
            new JProperty("Ammo",    s.ammo),
            new JProperty("Decay",   s.decay));

        private static ItemStack ParseItemStack(JToken t)
        {
            var stack = new ItemStack(t["Id"].Value<int>(), t["Count"].Value<int>());
            stack.slotIdx = t["SlotIdx"]?.Value<byte>() ?? 0;
            stack.ammo    = t["Ammo"]?.Value<int>()    ?? 0;
            stack.decay   = t["Decay"]?.Value<int>()   ?? 0;
            return stack;
        }

        // -----------------------------------------------------------------------
        // Container.Get — content, volume capacity, and decay factor
        // Payload: { "EntityId": n, "Pos": {"X":0,"Y":0,"Z":0} }
        // -----------------------------------------------------------------------
        public async Task Get(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    var content = container.GetContent();
                    var contentArray = new JArray();
                    if (content != null)
                        foreach (var item in content)
                            contentArray.Add(ItemStackJson(item));

                    json = new JObject(
                        new JProperty("EntityId",       entityId),
                        new JProperty("Pos",            MessageHelpers.Vec(pos)),
                        new JProperty("VolumeCapacity", container.VolumeCapacity),
                        new JProperty("DecayFactor",    container.DecayFactor),
                        new JProperty("Content",        contentArray));
                    await Task.CompletedTask;
                });

                if (json != null)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.Contains — whether the container holds at least one of an item type
        // Payload: { "EntityId": n, "Pos": {...}, "Type": itemTypeId }
        // -----------------------------------------------------------------------
        public async Task Contains(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    bool result = container.Contains(type);
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Type",     type),
                        new JProperty("Contains", result));
                    await Task.CompletedTask;
                });

                if (json != null)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.GetTotalItems — total count of a given item type across all slots
        // Payload: { "EntityId": n, "Pos": {...}, "Type": itemTypeId }
        // -----------------------------------------------------------------------
        public async Task GetTotalItems(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                JObject json = null;
                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    int count = container.GetTotalItems(type);
                    json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Type",     type),
                        new JProperty("Count",    count));
                    await Task.CompletedTask;
                });

                if (json != null)
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.AddItems — add count items of type; returns count that could not be added
        // Payload: { "EntityId": n, "Pos": {...}, "Type": itemTypeId, "Count": n }
        // -----------------------------------------------------------------------
        public async Task AddItems(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();
                int count    = args["Count"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    int couldNotAdd = container.AddItems(type, count);
                    var json = new JObject(
                        new JProperty("EntityId",    entityId),
                        new JProperty("Pos",         MessageHelpers.Vec(pos)),
                        new JProperty("Type",        type),
                        new JProperty("Count",       count),
                        new JProperty("CouldNotAdd", couldNotAdd));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.RemoveItems — remove count items of type; returns count actually removed
        // Payload: { "EntityId": n, "Pos": {...}, "Type": itemTypeId, "Count": n }
        // -----------------------------------------------------------------------
        public async Task RemoveItems(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);
                int type     = args["Type"].Value<int>();
                int count    = args["Count"].Value<int>();

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    int couldNotRemove = container.RemoveItems(type, count);
                    var json = new JObject(
                        new JProperty("EntityId",       entityId),
                        new JProperty("Pos",            MessageHelpers.Vec(pos)),
                        new JProperty("Type",           type),
                        new JProperty("Count",          count),
                        new JProperty("CouldNotRemove", couldNotRemove));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.Clear — remove all items from the container
        // Payload: { "EntityId": n, "Pos": {...} }
        // -----------------------------------------------------------------------
        public async Task Clear(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    container.Clear();
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)));
                    await _ctx.Messenger.SendAsync(MessageClass.Response, topic, json.ToString(Newtonsoft.Json.Formatting.None));
                });
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, MessageHelpers.ExceptionJson(ex));
            }
        }

        // -----------------------------------------------------------------------
        // Container.SetContent — replace all container contents (use responsibly)
        // Payload: { "EntityId": n, "Pos": {...}, "Content": [{Id,Count,SlotIdx?,Ammo?,Decay?}] }
        // -----------------------------------------------------------------------
        public async Task SetContent(string topic, string payload)
        {
            try
            {
                var args     = JObject.Parse(payload);
                int entityId = args["EntityId"].Value<int>();
                VectorInt3 pos = MessageHelpers.ParseVecInt3(args["Pos"]);

                var contentArray = args["Content"] as JArray
                    ?? throw new ArgumentException("Content must be a JSON array");
                var items = new List<ItemStack>();
                foreach (var t in contentArray)
                    items.Add(ParseItemStack(t));

                var structure = await GetStructure(topic, entityId);
                if (structure == null) return;

                await _ctx.MainThreadRunner.RunOnMainThread(async () =>
                {
                    var container = structure.GetDevice<Eleon.Modding.IContainer>(pos);
                    if (container == null)
                    {
                        await _ctx.Messenger.SendAsync(MessageClass.Exception, topic,
                            MessageHelpers.ErrorJson($"No container device at position {MessageHelpers.Vec(pos)} in entity {entityId}"));
                        return;
                    }
                    container.SetContent(items);
                    var json = new JObject(
                        new JProperty("EntityId", entityId),
                        new JProperty("Pos",      MessageHelpers.Vec(pos)),
                        new JProperty("Count",    items.Count));
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
