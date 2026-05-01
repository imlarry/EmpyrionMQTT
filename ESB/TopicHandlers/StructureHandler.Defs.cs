using Eleon.Modding;
using ESB.Helpers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ESB.TopicHandlers
{
    public partial class StructureHandler
    {
        // -------------------------------------------------------------------------
        // Shared serialization helpers
        // -------------------------------------------------------------------------

        static JObject ColorJson(Color c) => new JObject(
            new JProperty("R", c.r),
            new JProperty("G", c.g),
            new JProperty("B", c.b),
            new JProperty("A", c.a));

        static Color ParseColor(JToken t) => new Color(
            t["R"]?.Value<float>() ?? 0f,
            t["G"]?.Value<float>() ?? 0f,
            t["B"]?.Value<float>() ?? 0f,
            t["A"]?.Value<float>() ?? 1f);

        static JObject ItemStackJson(ItemStack s) => new JObject(
            new JProperty("Id",      s.id),
            new JProperty("Count",   s.count),
            new JProperty("SlotIdx", s.slotIdx),
            new JProperty("Ammo",    s.ammo),
            new JProperty("Decay",   s.decay));

        static ItemStack ParseItemStack(JToken t)
        {
            var stack    = new ItemStack(t["Id"].Value<int>(), t["Count"].Value<int>());
            stack.slotIdx = t["SlotIdx"]?.Value<byte>() ?? 0;
            stack.ammo    = t["Ammo"]?.Value<int>()    ?? 0;
            stack.decay   = t["Decay"]?.Value<int>()   ?? 0;
            return stack;
        }

        static JObject TankJson(IStructureTank tank)
        {
            if (tank == null) return null;
            return new JObject(
                new JProperty("Capacity",           tank.Capacity),
                new JProperty("Content",            tank.Content),
                new JProperty("UsesIntegerAmounts", tank.UsesIntegerAmounts));
        }

        static JObject BuildStructureJson(GlobalStructureInfo s) =>
            new JObject(
                new JProperty("Id",             s.id),
                new JProperty("Name",           s.name),
                new JProperty("FactionId",      s.factionId),
                new JProperty("FactionGroup",   s.factionGroup),
                new JProperty("ClassNr",        s.classNr),
                new JProperty("CoreType",       s.coreType),
                new JProperty("Type",           s.type),
                new JProperty("PlayfieldName",  s.PlayfieldName),
                new JProperty("Pos",            MessageHelpers.Vec(new Vector3(s.pos.x, s.pos.y, s.pos.z))),
                new JProperty("Rot",            MessageHelpers.Vec(new Vector3(s.rot.x, s.rot.y, s.rot.z))),
                new JProperty("LastVisitedUtc", s.lastVisitedUTC),
                new JProperty("Powered",        s.powered),
                new JProperty("DockedShips",    s.dockedShips != null
                                                    ? (JToken)new JArray(s.dockedShips)
                                                    : JValue.CreateNull()));

        static JArray ItemStacksJson(List<ItemStack> stacks)
        {
            var arr = new JArray();
            if (stacks == null) return arr;
            foreach (var s in stacks)
                arr.Add(ItemStackJson(s));
            return arr;
        }
    }
}
