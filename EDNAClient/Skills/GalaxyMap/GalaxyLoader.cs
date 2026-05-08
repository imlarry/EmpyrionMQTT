using System.IO;
using EDNAClient.Core;

namespace EDNAClient.Skills.GalaxyMap
{
    internal static class GalaxyLoader
    {
        public static List<StarSystem> Load(string csvPath)
        {
            var result = new List<StarSystem>();
            if (!File.Exists(csvPath))
            {
                EdnaLogger.Log($"GalaxyLoader: file not found: {csvPath}");
                return result;
            }

            bool first = true;
            foreach (var line in File.ReadLines(csvPath))
            {
                if (first) { first = false; continue; }
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new char[] { ',' }, 4);
                if (parts.Length < 4) continue;

                if (!int.TryParse(parts[0].Trim(), out int x)) continue;
                if (!int.TryParse(parts[1].Trim(), out int y)) continue;
                if (!int.TryParse(parts[2].Trim(), out int z)) continue;
                string name = parts[3].Trim();

                EdnaLogger.Detail($"[GalaxyLoader] row: {name} ({x},{y},{z})");
                result.Add(new StarSystem(x, y, z, name));
            }

            EdnaLogger.Log($"GalaxyLoader: loaded {result.Count} systems from {csvPath}");
            return result;
        }
    }
}
