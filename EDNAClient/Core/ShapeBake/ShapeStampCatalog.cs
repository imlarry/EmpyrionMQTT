using System;
using System.Collections.Generic;
using System.IO;

namespace EDNAClient.Core.ShapeBake
{
    // Singleton catalog of shape stamps loaded once at app startup from
    // shapes.bake (shipped next to EDNA.exe via the csproj Content include).
    // TomographyScanner queries this during Build() to resolve per-block
    // sub-voxel occupancy for the Sharp (VoxelCubes) renderer.
    public static class ShapeStampCatalog
    {
        private static readonly Dictionary<string, BakedStamp> _byName =
            new Dictionary<string, BakedStamp>(StringComparer.Ordinal);
        private static int _resolution;
        private static bool _loaded;

        public static bool IsLoaded => _loaded;
        public static int Resolution => _resolution;
        public static int Count => _byName.Count;

        public static BakedStamp? GetStamp(string name) =>
            _byName.TryGetValue(name, out var s) ? s : null;

        public static IEnumerable<BakedStamp> All => _byName.Values;

        public static bool TryLoadFromBaseDirectory(out string error)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "shapes.bake");
                if (!File.Exists(path))
                {
                    error = "shapes.bake not found at " + path;
                    return false;
                }
                LoadFromFile(path);
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
            var file = BakeReader.Read(path);
            _byName.Clear();
            foreach (var s in file.Stamps) _byName[s.Name] = s;
            _resolution = file.Resolution;
            _loaded = true;
        }

        public static string Summarize() =>
            $"ShapeStampCatalog: {_byName.Count} stamps at {_resolution}^3";
    }
}
