using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EDNAClient.Core
{
    // High-level kind of a block, derived from the `Shape:` field in BlocksConfig.ecf.
    // Determines how the block participates in rendering / scanning.
    public enum BlockShapeKind
    {
        Unknown,
        New,         // building block -- has a ChildShapes list indexed by IBlock.Shape
        ModelEntity, // device -- fixed Unity prefab (Model: @models/...)
        Terrain,     // natural terrain (IDs 1..255 typically)
        Invisible,   // air, group-umbrella entries, etc.
    }

    // Parsed block definition from BlocksConfig.ecf.
    public sealed class BlockDef
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = "";
        public string Ref         { get; set; } = "";
        public BlockShapeKind Shape { get; set; }
        public string Model       { get; set; } = "";
        public string Material    { get; set; } = "";
        public string Category    { get; set; } = "";
        public string Class       { get; set; } = "";

        // Ordered list of shape names; index = the per-block Shape integer
        // returned by the game's IBlock.Get(...). Empty for non-structural blocks.
        public string[] ChildShapes { get; set; } = Array.Empty<string>();

        // Names of placeable child blocks under a group umbrella (e.g. WoodBlocks
        // -> WoodFull, WoodThin, ...). Empty for non-umbrella entries.
        public string[] ChildBlocks { get; set; } = Array.Empty<string>();

        // True for placeable building blocks. These are the entries whose Shape
        // integer maps into ChildShapes; floors / walls / hull / etc. all use this.
        public bool IsStructural => Shape == BlockShapeKind.New;
    }

    // Catalog of block definitions loaded from BlocksConfig.ecf.
    //
    // The file lives at {GameRoot}/Content/Configuration/BlocksConfig.ecf. Each
    // entry is a `{ Block Id: N, Name: Foo, ... }` block; building blocks declare
    // a `ChildShapes:` list of shape names that the per-block Shape integer
    // indexes into. Ref: entries inherit properties from a previously-defined
    // block so a Thin variant can share ChildShapes with its Full parent without
    // restating them.
    //
    // BlockShapesWindow.ecf is NOT needed for shape resolution -- its data is
    // only the building-UI palette layout.
    public static class BlocksConfig
    {
        private static readonly Dictionary<int, BlockDef>    _byId   = new();
        private static readonly Dictionary<string, BlockDef> _byName = new();

        // ── Public API ────────────────────────────────────────────────────────

        public static int Count => _byId.Count;

        public static IEnumerable<BlockDef> All => _byId.Values;

        public static BlockDef? GetById(int id) =>
            _byId.TryGetValue(id, out var d) ? d : null;

        public static BlockDef? GetByName(string name) =>
            _byName.TryGetValue(name, out var d) ? d : null;

        // ChildShapes list for the block at `id`, or an empty array if the block
        // is not structural / not in the catalog.
        public static string[] ShapeListFor(int id) =>
            GetById(id)?.ChildShapes ?? Array.Empty<string>();

        // Resolve a (Type ID, Shape index) pair into the canonical shape name
        // (e.g. "Cube", "CornerHalfA3", "WallSlopedRound"). Returns null if the
        // block is not structural or the index is out of range.
        public static string? ResolveShape(int id, int shapeIndex)
        {
            var list = ShapeListFor(id);
            if (shapeIndex < 0 || shapeIndex >= list.Length) return null;
            return list[shapeIndex];
        }

        public static bool IsStructural(int id) =>
            GetById(id)?.IsStructural ?? false;

        public static IEnumerable<int> AllStructuralIds()
        {
            foreach (var kv in _byId)
                if (kv.Value.IsStructural) yield return kv.Key;
        }

        // ── Loading ───────────────────────────────────────────────────────────

        public static void LoadFromFile(string path)
        {
            _byId.Clear();
            _byName.Clear();

            var defs = ParseFile(path).ToList();
            foreach (var d in defs)
            {
                if (d.Id > 0) _byId[d.Id] = d;
                if (!string.IsNullOrEmpty(d.Name)) _byName[d.Name] = d;
            }

            // Resolve Ref inheritance after the initial pass so order doesn't matter.
            foreach (var d in defs)
                ResolveRef(d, new HashSet<string>());
        }

        // Attempts to find BlocksConfig.ecf relative to EDNA's deployed location.
        // EDNA.exe deploys to {GameRoot}/Content/Mods/ESB/EDNA/, so the game root
        // is four directories above the executable's folder.
        public static bool TryLoadFromEmpyrionInstall(out string error)
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var gameRoot = Path.GetFullPath(
                    Path.Combine(exeDir, "..", "..", "..", ".."));
                var ecfPath = Path.Combine(
                    gameRoot, "Content", "Configuration", "BlocksConfig.ecf");

                if (!File.Exists(ecfPath))
                {
                    error = "BlocksConfig.ecf not found at " + ecfPath;
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

        public static string Summarize()
        {
            int structural = 0, devices = 0, terrain = 0, invisible = 0, unknown = 0;
            int withShapeList = 0;
            foreach (var d in _byId.Values)
            {
                switch (d.Shape)
                {
                    case BlockShapeKind.New:         structural++; break;
                    case BlockShapeKind.ModelEntity: devices++;    break;
                    case BlockShapeKind.Terrain:     terrain++;    break;
                    case BlockShapeKind.Invisible:   invisible++;  break;
                    default:                         unknown++;    break;
                }
                if (d.ChildShapes.Length > 0) withShapeList++;
            }
            return
                $"BlocksConfig: {_byId.Count} blocks -- " +
                $"{structural} structural / {devices} devices / " +
                $"{terrain} terrain / {invisible} invisible / {unknown} unknown; " +
                $"{withShapeList} have ChildShapes.";
        }

        // ── Ref resolution ────────────────────────────────────────────────────

        private static void ResolveRef(BlockDef d, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(d.Ref)) return;
            if (!seen.Add(d.Name)) return; // cycle guard
            if (!_byName.TryGetValue(d.Ref, out var parent)) return;

            ResolveRef(parent, seen);

            if (d.Shape == BlockShapeKind.Unknown)  d.Shape       = parent.Shape;
            if (string.IsNullOrEmpty(d.Model))      d.Model       = parent.Model;
            if (string.IsNullOrEmpty(d.Material))   d.Material    = parent.Material;
            if (string.IsNullOrEmpty(d.Category))   d.Category    = parent.Category;
            if (string.IsNullOrEmpty(d.Class))      d.Class       = parent.Class;
            if (d.ChildShapes.Length == 0)          d.ChildShapes = parent.ChildShapes;
            if (d.ChildBlocks.Length == 0)          d.ChildBlocks = parent.ChildBlocks;
        }

        // ── Parser ────────────────────────────────────────────────────────────
        //
        // The file is line-oriented. Each top-level block is `{ Block ... }` with
        // optional nested `{ Child ... }` entries we ignore. We track brace depth
        // by counting { and } per line (outside of quoted strings) and capture
        // depth-1 property lines into the current BlockDef.

        private static IEnumerable<BlockDef> ParseFile(string path)
        {
            int depth = 0;
            BlockDef? current = null;

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0) continue;

                int opens  = CountChar(line, '{');
                int closes = CountChar(line, '}');

                if (depth == 0 && opens > 0)
                {
                    // Top-level block opening. The header text after the first
                    // '{' contains "Block Id: N, Name: Foo" (or Block Name + Ref).
                    var openIdx = line.IndexOf('{');
                    var header  = line.Substring(openIdx + 1).Trim();
                    current = new BlockDef();
                    ParseKvPairs(header, current);
                    depth += opens - closes;

                    if (depth <= 0)
                    {
                        // Defensive: degenerate single-line block.
                        yield return current;
                        current = null;
                        depth = 0;
                    }
                    continue;
                }

                if (depth >= 1)
                {
                    // Direct property of the top-level block (depth becomes >= 2
                    // when we descend into a nested { Child ... } sub-block).
                    if (depth == 1 && opens == 0 && closes == 0 && current != null)
                        ParseKvPairs(line, current);

                    depth += opens - closes;
                    if (depth == 0 && current != null)
                    {
                        yield return current;
                        current = null;
                    }
                }
            }
        }

        // Strip `#`-to-end-of-line comments, respecting quoted strings.
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

        // Parse "key: value" pairs from a line. Header lines carry multiple
        // pairs ("Block Id: 394, Name: HullArmoredFull"); property lines carry
        // one pair followed by metadata modifiers we discard
        // ("HitPoints: 200, type: int, display: true").
        private static void ParseKvPairs(string text, BlockDef def)
        {
            var modifierKeys = ModifierKeyNames;
            foreach (var tok in SplitTopLevelCommas(text))
            {
                var trimmed = tok.Trim();
                if (trimmed.Length == 0) continue;

                int colon = trimmed.IndexOf(':');
                if (colon <= 0) continue;

                var key = trimmed.Substring(0, colon).Trim();
                var val = trimmed.Substring(colon + 1).Trim();

                if (key.StartsWith("+")) key = key.Substring(1).TrimStart();

                if (modifierKeys.Contains(key)) continue;

                if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                    val = val.Substring(1, val.Length - 2);

                ApplyKv(def, key, val);
            }
        }

        // Comma tokens we always discard (they are property-decoration metadata,
        // not actual block fields).
        private static readonly HashSet<string> ModifierKeyNames = new()
        {
            "type", "display", "formatter", "min", "max", "step",
        };

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

        private static void ApplyKv(BlockDef def, string key, string value)
        {
            switch (key)
            {
                case "Block Id":
                    if (int.TryParse(value, out var id)) def.Id = id;
                    break;
                case "Block Name":
                case "Name":
                    def.Name = value;
                    break;
                case "Ref":
                    def.Ref = value;
                    break;
                case "Shape":
                    def.Shape = ParseShapeKind(value);
                    break;
                case "Model":
                    def.Model = value;
                    break;
                case "Material":
                    def.Material = value;
                    break;
                case "Category":
                    def.Category = value;
                    break;
                case "Class":
                    def.Class = value;
                    break;
                case "ChildShapes":
                    def.ChildShapes = SplitCommaList(value);
                    break;
                case "ChildBlocks":
                    def.ChildBlocks = SplitCommaList(value);
                    break;
                // Everything else (HitPoints, Mass, Texture, AllowPlacingAt, ...)
                // is irrelevant to the shape catalog and gets dropped.
            }
        }

        private static BlockShapeKind ParseShapeKind(string s)
        {
            switch (s)
            {
                case "New":         return BlockShapeKind.New;
                case "ModelEntity": return BlockShapeKind.ModelEntity;
                case "Terrain":     return BlockShapeKind.Terrain;
                case "Invisible":   return BlockShapeKind.Invisible;
                default:            return BlockShapeKind.Unknown;
            }
        }

        private static string[] SplitCommaList(string s)
        {
            var parts = s.Split(',');
            var result = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++) result[i] = parts[i].Trim();
            return result;
        }
    }
}
