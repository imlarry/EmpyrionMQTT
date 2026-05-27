namespace EmpyrionMQTT.ShapeBaker;

// Extracts the set of single-cell window/door/walkway prefab names to bake.
// Driven by BlocksConfig.ecf: each block has a Model path of the form
//   @models/Blocks/<Class>/Standard/<PrefabName>
// We collect the last path segment for every block whose Name starts with
// Window / Door / Walkway / Hangar AND whose SizeInBlocks is absent or
// "1,1,1". Multi-cell blocks (e.g. Window_v1x2 with SizeInBlocks "1,2,1")
// are skipped on purpose -- at render time they reuse the 1x1 sibling's
// stamp via cross-cell tiling.
internal sealed class BlockEntry
{
    public string Name         = "";
    public string Ref          = "";
    public string Model        = "";
    public string SizeInBlocks = "";
}

internal static class BlocksConfigPrefabReader
{
    public static HashSet<string> ReadPrefabNames(string ecfPath)
    {
        var all = ParseAll(ecfPath);

        // Resolve Ref inheritance for Model and SizeInBlocks so blocks that
        // only override unrelated fields still see their parent's Model.
        foreach (var e in all.Values)
            ResolveRef(e, all, new HashSet<string>());

        var prefabs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in all.Values)
        {
            if (!IsRelevantCategory(e.Name)) continue;
            if (!IsOneCellSize(e.SizeInBlocks)) continue;
            if (string.IsNullOrEmpty(e.Model)) continue;

            var prefab = ExtractPrefabName(e.Model);
            if (!string.IsNullOrEmpty(prefab))
                prefabs.Add(prefab);
        }
        return prefabs;
    }

    private static bool IsRelevantCategory(string name) =>
        name.StartsWith("Window",  StringComparison.Ordinal) ||
        name.StartsWith("Door",    StringComparison.Ordinal) ||
        name.StartsWith("Walkway", StringComparison.Ordinal) ||
        name.StartsWith("Hangar",  StringComparison.Ordinal);

    private static bool IsOneCellSize(string size) =>
        string.IsNullOrEmpty(size) || size == "1,1,1";

    private static string ExtractPrefabName(string model)
    {
        int slash = model.LastIndexOf('/');
        return slash >= 0 ? model.Substring(slash + 1) : model;
    }

    private static void ResolveRef(BlockEntry e, Dictionary<string, BlockEntry> all, HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(e.Ref)) return;
        if (!visited.Add(e.Name)) return;
        if (!all.TryGetValue(e.Ref, out var parent)) return;
        ResolveRef(parent, all, visited);

        if (string.IsNullOrEmpty(e.Model))        e.Model        = parent.Model;
        if (string.IsNullOrEmpty(e.SizeInBlocks)) e.SizeInBlocks = parent.SizeInBlocks;
    }

    // Line-oriented parser. Each top-level block is `{ Block ... }` with
    // optional nested `{ Child ... }` entries we ignore. Brace depth is
    // tracked per line, ignoring braces inside quoted strings.
    private static Dictionary<string, BlockEntry> ParseAll(string ecfPath)
    {
        var byName = new Dictionary<string, BlockEntry>(StringComparer.Ordinal);
        int depth = 0;
        BlockEntry? current = null;

        foreach (var rawLine in File.ReadLines(ecfPath))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;

            int opens  = CountChar(line, '{');
            int closes = CountChar(line, '}');

            if (depth == 0 && opens > 0)
            {
                int openIdx = line.IndexOf('{');
                var header  = line.Substring(openIdx + 1).Trim();
                current = new BlockEntry();
                ApplyKvPairs(header, current);
                depth += opens - closes;

                if (depth <= 0 && current != null)
                {
                    if (!string.IsNullOrEmpty(current.Name))
                        byName[current.Name] = current;
                    current = null;
                    depth = 0;
                }
                continue;
            }

            if (depth >= 1)
            {
                if (depth == 1 && opens == 0 && closes == 0 && current != null)
                    ApplyKvPairs(line, current);

                depth += opens - closes;
                if (depth == 0 && current != null)
                {
                    if (!string.IsNullOrEmpty(current.Name))
                        byName[current.Name] = current;
                    current = null;
                }
            }
        }
        return byName;
    }

    private static void ApplyKvPairs(string text, BlockEntry e)
    {
        foreach (var tok in SplitTopLevelCommas(text))
        {
            var trimmed = tok.Trim();
            if (trimmed.Length == 0) continue;

            int colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;

            var key = trimmed.Substring(0, colon).Trim();
            var val = trimmed.Substring(colon + 1).Trim();

            if (key.StartsWith("+")) key = key.Substring(1).TrimStart();

            // Drop type/display/formatter modifier suffixes that follow values.
            if (key == "type" || key == "display" || key == "formatter" ||
                key == "min"  || key == "max"     || key == "step") continue;

            if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                val = val.Substring(1, val.Length - 2);

            switch (key)
            {
                case "Block Name":
                case "Name":         e.Name         = val; break;
                case "Ref":          e.Ref          = val; break;
                case "Model":        e.Model        = val; break;
                case "SizeInBlocks": e.SizeInBlocks = val; break;
            }
        }
    }

    private static List<string> SplitTopLevelCommas(string text)
    {
        var result = new List<string>();
        int start = 0;
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        result.Add(text.Substring(start));
        return result;
    }

    private static string StripComment(string line)
    {
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == '#' && !inQuotes) return line.Substring(0, i);
        }
        return line;
    }

    private static int CountChar(string s, char ch)
    {
        int count = 0;
        bool inQuotes = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ch && !inQuotes) count++;
        }
        return count;
    }
}
