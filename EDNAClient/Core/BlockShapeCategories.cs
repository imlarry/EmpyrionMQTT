using System;
using System.Collections.Generic;
using System.IO;

namespace EDNAClient.Core
{
    // One TabGroup parsed from BlockShapesWindow.ecf -- a UI palette category
    // (e.g. "Cubes and Ramps") with its ordered, deduplicated list of shape
    // names. Each TabGroup may contain multiple { Child N ... Shapes: "..." }
    // sub-blocks (one per grid size); ShapeNames is the union across them in
    // declaration order.
    public sealed class BlockShapeCategory
    {
        public int    Id   { get; }
        public string Name { get; }
        public string[] ShapeNames { get; }

        public BlockShapeCategory(int id, string name, string[] shapeNames)
        {
            Id = id; Name = name; ShapeNames = shapeNames;
        }
    }

    // Catalog of TabGroup categories loaded from
    // {GameRoot}/Content/Configuration/BlockShapesWindow.ecf. The file
    // organizes shape names into the in-game building-UI tabs; we re-use that
    // grouping to break up the Tomography Shape Gallery into per-category
    // submenu entries.
    //
    // Inline layout tokens (<newline>, <pagebreak>, <empty>) are dropped so
    // ShapeNames contains only real shape identifiers.
    public static class BlockShapeCategories
    {
        private static List<BlockShapeCategory> _all = new List<BlockShapeCategory>();
        private static bool _loaded;

        public static bool IsLoaded => _loaded;
        public static IReadOnlyList<BlockShapeCategory> All => _all;

        public static BlockShapeCategory? GetById(int id)
        {
            foreach (var c in _all) if (c.Id == id) return c;
            return null;
        }

        public static bool TryLoadFromEmpyrionInstall(out string error)
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var gameRoot = Path.GetFullPath(
                    Path.Combine(exeDir, "..", "..", "..", ".."));
                var ecfPath = Path.Combine(
                    gameRoot, "Content", "Configuration", "BlockShapesWindow.ecf");

                if (!File.Exists(ecfPath))
                {
                    error = "BlockShapesWindow.ecf not found at " + ecfPath;
                    return false;
                }

                LoadFromFile(ecfPath);
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void LoadFromFile(string path)
        {
            _all = ParseFile(path);
            _loaded = true;
        }

        public static string Summarize()
        {
            int total = 0;
            foreach (var c in _all) total += c.ShapeNames.Length;
            return $"BlockShapeCategories: {_all.Count} categories, {total} shape entries";
        }

        // Line-oriented parser, brace-depth tracked. Top-level blocks start with
        // "{ TabGroup Id: N" and may contain nested { Child M ... } sub-blocks.
        // We accumulate Name from the TabGroup header and union every
        // Shapes: "..." string we see at any depth inside the current group.
        private static List<BlockShapeCategory> ParseFile(string path)
        {
            var result = new List<BlockShapeCategory>();
            int depth = 0;
            int currentId = -1;
            string currentName = "";
            List<string>? currentShapes = null;
            HashSet<string>? currentSeen = null;

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0) continue;

                int opens  = CountChar(line, '{');
                int closes = CountChar(line, '}');

                if (depth == 0 && opens > 0 && line.Contains("TabGroup"))
                {
                    currentId = ParseInt(AfterToken(line, "Id:"));
                    currentName = "";
                    currentShapes = new List<string>();
                    currentSeen = new HashSet<string>(StringComparer.Ordinal);
                    depth += opens - closes;
                    continue;
                }

                if (depth >= 1 && currentShapes != null)
                {
                    if (line.StartsWith("Name:"))
                    {
                        currentName = ExtractQuoted(line);
                    }
                    else if (line.StartsWith("Shapes:"))
                    {
                        AddShapes(ExtractQuoted(line), currentShapes, currentSeen!);
                    }

                    depth += opens - closes;
                    if (depth == 0)
                    {
                        if (currentId >= 0 && currentName.Length > 0 && currentShapes.Count > 0)
                            result.Add(new BlockShapeCategory(currentId, currentName, currentShapes.ToArray()));
                        currentId = -1;
                        currentName = "";
                        currentShapes = null;
                        currentSeen = null;
                    }
                    continue;
                }

                depth += opens - closes;
                if (depth < 0) depth = 0;
            }

            return result;
        }

        private static void AddShapes(string raw, List<string> dest, HashSet<string> seen)
        {
            if (raw.Length == 0) return;
            foreach (var tok in raw.Split(','))
            {
                var t = tok.Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("<") && t.EndsWith(">")) continue;   // <newline> / <pagebreak> / <empty>
                if (seen.Add(t)) dest.Add(t);
            }
        }

        private static string ExtractQuoted(string line)
        {
            int q1 = line.IndexOf('"');
            if (q1 < 0) return "";
            int q2 = line.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return line.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string AfterToken(string line, string token)
        {
            int i = line.IndexOf(token, StringComparison.Ordinal);
            if (i < 0) return "";
            return line.Substring(i + token.Length).Trim();
        }

        private static int ParseInt(string s)
        {
            int n = 0;
            int i = 0;
            while (i < s.Length && (s[i] < '0' || s[i] > '9')) i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') { n = n * 10 + (s[i] - '0'); i++; }
            return n;
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
}
