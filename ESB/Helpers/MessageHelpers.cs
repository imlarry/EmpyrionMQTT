using Eleon.Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ESB.Helpers
{
    internal static class MessageHelpers
    {
        private sealed class PascalCaseContractResolver : DefaultContractResolver
        {
            protected override string ResolvePropertyName(string propertyName)
            {
                if (string.IsNullOrEmpty(propertyName)) return propertyName;
                return char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1);
            }
        }

        internal static readonly JsonSerializerSettings PascalCaseSettings =
            new JsonSerializerSettings { ContractResolver = new PascalCaseContractResolver() };


        /// <summary>Returns a JSON error payload for a known/expected error condition.</summary>
        internal static string ErrorJson(string message) =>
            new JObject(
                new JProperty("Error", message)
            ).ToString(Newtonsoft.Json.Formatting.None);

        /// <summary>Returns a JSON error payload for a caught exception, including the exception type.</summary>
        internal static string ExceptionJson(Exception ex) =>
            new JObject(
                new JProperty("Error", ex.Message),
                new JProperty("ExceptionType", ex.GetType().Name)
            ).ToString(Newtonsoft.Json.Formatting.None);

        // --- Vector serialization (output) ---

        internal static JObject Vec(Vector3 v) =>
            new JObject(new JProperty("X", v.x), new JProperty("Y", v.y), new JProperty("Z", v.z));

        internal static JObject Vec(VectorInt3 v) =>
            new JObject(new JProperty("X", v.x), new JProperty("Y", v.y), new JProperty("Z", v.z));

        internal static JObject Vec(Quaternion q) =>
            new JObject(new JProperty("X", q.x), new JProperty("Y", q.y), new JProperty("Z", q.z), new JProperty("W", q.w));

        // --- Vector parsing (input) -- expects {"X":0,"Y":0,"Z":0} ---

        internal static Vector3    ParseVec3    (JToken t) => new Vector3   (t["X"].Value<float>(), t["Y"].Value<float>(), t["Z"].Value<float>());
        internal static VectorInt3 ParseVecInt3 (JToken t) => new VectorInt3(t["X"].Value<int>(),   t["Y"].Value<int>(),   t["Z"].Value<int>());
        internal static Quaternion ParseQuat    (JToken t) => new Quaternion((float)t["X"], (float)t["Y"], (float)t["Z"], (float)t["W"]);

        // --- Tabular array helper ---

        // Compact encoding for arrays of homogeneous records; see Docs/TopicSchema.md.
        internal static JObject Tabular(string[] columns, JArray rows)
        {
            var colsArr = new JArray();
            for (int i = 0; i < columns.Length; i++) colsArr.Add(columns[i]);
            return new JObject(
                new JProperty("Columns", colsArr),
                new JProperty("Rows",    rows));
        }

        // --- Item stack serialization ---

        private static readonly string[] NamedItemStackColumns =
            { "Id", "Name", "Count", "SlotIdx", "Ammo", "Decay" };

        internal static JObject ItemStacksJson(List<ItemStack> items, Dictionary<int, string> nameMap)
        {
            var rows = new JArray();
            foreach (var item in items)
            {
                if (!nameMap.TryGetValue(item.id, out string name))
                    name = "<no mapping>";
                rows.Add(new JArray(item.id, name, item.count, item.slotIdx, item.ammo, item.decay));
            }
            return Tabular(NamedItemStackColumns, rows);
        }
    }
}
