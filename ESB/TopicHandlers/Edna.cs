using Eleon.Modding;
using ESB.Common;
using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ESB.TopicHandlers
{
    public class Edna
    {
        private readonly ContextData _ctx;

        public Edna(ContextData ctx)
        {
            _ctx = ctx;
        }

        public async Task Subscribe()
        {
            await _ctx.Messenger.SubscribeAsync("Edna.DumpMemory", DumpMemory);
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

        public async Task StructureContainers(string topic, string payload)
        {
            try
            {
                int trigger = 0;
                if (trigger == 0)
                {
                    // this is a command for the edna bot
                    var command = "a string";

                    await _ctx.Messenger.SendAsync("Self/Q/Edna.TestSelf/<Cntxt.ClientId>/<interlocked pubSeqId>", command);

                    var containerPositions = _ctx.ModApi.Application.LocalPlayer.CurrentStructure.GetDevices(DeviceTypeName.Container);   // get all containers in current structure
                    var containerArray = new JArray();                                                                                  // init empty array for json of containers
                    for (int i = 0; i < containerPositions.Count; i++)
                    {
                        IContainer container = _ctx.ModApi.Application.LocalPlayer.CurrentStructure.GetDevice<IContainer>(containerPositions.GetAt(i));
                        if (container != null)
                        {
                            var contents = container.GetContent();
                            JArray contentArray = new JArray();
                            foreach (var slot in contents)
                            {
                                JObject slotObject = new JObject(
                                    new JProperty("Id", slot.id),
                                    new JProperty("Count", slot.count),
                                    new JProperty("SlotIdx", slot.slotIdx),
                                    new JProperty("Ammo", slot.ammo),
                                    new JProperty("Decay", slot.decay)
                                    );
                                contentArray.Add(slotObject);
                            }
                            var containerProperties = _ctx.ModApi.Application.LocalPlayer.CurrentStructure.GetBlock(containerPositions.GetAt(i));
                            var containerObject = new JObject(
                                new JProperty("CustomName", containerProperties.CustomName ?? "DefaultName"),
                                new JProperty("Content", contentArray)
                                );
                            containerArray.Add(containerObject);
                        }
                    }
                    JObject edna = new JObject(
                        new JProperty("GameTicks", _ctx.ModApi.Application.GameTicks),
                        new JProperty("LocalPlayerName", _ctx.ModApi.Application.LocalPlayer.Name),
                        new JProperty("CurrentStructureEntityName", _ctx.ModApi.Application.LocalPlayer.CurrentStructure.Entity.Name),
                        new JProperty("StorageContainers", containerArray)
                        );
                    await _ctx.Messenger.SendAsync(MessageClass.Event, "Edna.Response", edna.ToString());  // Newtonsoft.Json.Formatting.None
                }
            }
            catch (Exception ex)
            {
                await _ctx.Messenger.SendAsync(MessageClass.Exception, topic, ex.Message);
            }
        }

    }
}
