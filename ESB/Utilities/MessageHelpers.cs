using Eleon.Modding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ESB
{
    internal static class MessageHelpers
    {
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

        // --- Item stack serialization ---

        internal static JArray ItemStacksJson(List<ItemStack> items, Dictionary<int, string> nameMap)
        {
            var result = new JArray();
            foreach (var item in items)
            {
                if (!nameMap.TryGetValue(item.id, out string name))
                    name = "<no mapping>";

                result.Add(new JObject(
                    new JProperty("Id",      item.id),
                    new JProperty("Name",    name),
                    new JProperty("Count",   item.count),
                    new JProperty("SlotIdx", item.slotIdx),
                    new JProperty("Ammo",    item.ammo),
                    new JProperty("Decay",   item.decay)
                ));
            }
            return result;
        }
    }
}
