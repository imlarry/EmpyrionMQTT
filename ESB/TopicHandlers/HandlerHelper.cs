using Eleon.Modding;
using ESB.Helpers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace ESB.TopicHandlers
{
    internal static class HandlerHelper
    {
        // -------------------------------------------------------------------------
        // Shared serialization helpers (used across multiple handlers)
        // -------------------------------------------------------------------------

        internal static JObject BuildStructureJson(GlobalStructureInfo s) =>
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
                new JProperty("DockedShips",    s.dockedShips != null ? (JToken)new JArray(s.dockedShips) : JValue.CreateNull()));

        private static readonly string[] ItemStackColumns =
            { "Id", "Count", "SlotIdx", "Ammo", "Decay" };

        internal static JObject ItemStacksJson(List<ItemStack> stacks)
        {
            var rows = new JArray();
            if (stacks != null)
                foreach (var s in stacks)
                    rows.Add(new JArray(s.id, s.count, s.slotIdx, s.ammo, s.decay));
            return MessageHelpers.Tabular(ItemStackColumns, rows);
        }
    }
}
