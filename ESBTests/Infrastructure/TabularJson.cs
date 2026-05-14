using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ESBTests.Infrastructure;

// Reader for the {Columns, Rows} tabular payload convention (see Docs/TopicSchema.md section 9).
internal static class TabularJson
{
    public static IEnumerable<JObject> Rows(JToken? table)
    {
        if (table is not JObject obj) yield break;
        var cols = obj["Columns"] as JArray;
        var rows = obj["Rows"]    as JArray;
        if (cols == null || rows == null) yield break;
        foreach (var row in rows)
        {
            if (row is not JArray arr) continue;
            var item = new JObject();
            for (int i = 0; i < cols.Count && i < arr.Count; i++)
                item[(string)cols[i]!] = arr[i];
            yield return item;
        }
    }
}
