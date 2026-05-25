namespace EmpyrionMQTT.ShapeBaker;

// Verifies a baked shape file and emits a single high-signal report so
// we can answer "is the bake correct?" without iterating:
//
//   1. Header roundtrip (magic, version, resolution, frame).
//   2. Per-stamp fill count and percent for every stamp, sorted by fill%
//      so anomalies (0% empty, 100% over-filled non-cubes, etc.) cluster.
//   3. ASCII slice dumps for a canonical set of shapes whose geometry we
//      already know (Cube, CubeHalf, Wall, Beam, RampC, ...) so we can
//      eyeball whether voxelization landed correctly.
//   4. Top-line anomaly summary: empty stamps, Cube under-fill, etc.
internal static class VerifyMode
{
    // Canonical reference shapes whose expected occupancy we can describe in
    // a sentence; the verifier dumps their ASCII slices for visual check.
    private static readonly (string Name, string Expected)[] CanonicalShapes =
    {
        ("Cube",          "all voxels filled"),
        ("CubeHalf",      "lower half along one axis"),
        ("CubeQuarter",   "one quarter"),
        ("CubeEighth",    "one eighth (corner cube)"),
        ("Wall",          "thin Z-slab"),
        ("WallLow",       "thin slab, lower half"),
        ("Beam",          "narrow vertical column"),
        ("RampC",         "triangular wedge (ramp)"),
        ("RampA",         "triangular wedge (alt orientation)"),
        ("CornerHalfA3",  "corner cut"),
        ("EdgeRound",     "rounded edge along one axis"),
        ("Cylinder",      "round column"),
        ("PyramidA",      "pyramid"),
        ("SphereHalf",    "hemisphere"),
        ("CutCornerA",    "single corner cut"),
    };

    public static int Run(string bakePath)
    {
        if (!File.Exists(bakePath))
        {
            Console.Error.WriteLine($"bake file not found: {bakePath}");
            return 1;
        }

        BakeFile bake;
        try { bake = BakeReader.Read(bakePath); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"failed to read bake: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        int res = bake.Resolution;
        int totalVoxels = res * res * res;
        var info = new FileInfo(bakePath);

        Console.WriteLine($"=== bake summary ===");
        Console.WriteLine($"path        : {info.FullName}");
        Console.WriteLine($"size        : {info.Length} bytes");
        Console.WriteLine($"resolution  : {res}");
        Console.WriteLine($"stamp count : {bake.Stamps.Count}");
        Console.WriteLine($"frame min   : ({bake.Frame.Min.X:F3}, {bake.Frame.Min.Y:F3}, {bake.Frame.Min.Z:F3})");
        Console.WriteLine($"frame max   : ({bake.Frame.Max.X:F3}, {bake.Frame.Max.Y:F3}, {bake.Frame.Max.Z:F3})");
        Console.WriteLine();

        var stats = bake.Stamps
            .Select(s => (Stamp: s, Filled: BakeReader.CountFilled(s)))
            .ToList();

        var empty = stats.Where(t => t.Filled == 0).Select(t => t.Stamp.Name).ToList();
        var cube = stats.FirstOrDefault(t => t.Stamp.Name == "Cube");
        var cubeFillPct = cube.Stamp == null ? -1 : 100.0 * cube.Filled / totalVoxels;

        Console.WriteLine("=== anomalies ===");
        Console.WriteLine($"empty stamps        : {empty.Count}"
            + (empty.Count > 0 ? " -> " + string.Join(", ", empty.Take(20)) + (empty.Count > 20 ? ", ..." : "") : ""));
        if (cube.Stamp != null)
            Console.WriteLine($"Cube fill           : {cube.Filled}/{totalVoxels} ({cubeFillPct:F1}%) -- expect ~100%");
        else
            Console.WriteLine("Cube fill           : (Cube stamp missing!)");

        var overFilled = stats.Where(t => t.Stamp.Name != "Cube" && t.Filled == totalVoxels).Select(t => t.Stamp.Name).ToList();
        Console.WriteLine($"100%-filled non-Cube: {overFilled.Count}"
            + (overFilled.Count > 0 ? " -> " + string.Join(", ", overFilled.Take(20)) : ""));
        Console.WriteLine();

        // Full table sorted by fill % so outliers cluster at top and bottom.
        Console.WriteLine("=== all stamps (sorted by fill%) ===");
        Console.WriteLine($"{"name",-32} {"fill%",6}  voxels");
        foreach (var (stamp, filled) in stats.OrderByDescending(t => t.Filled).ThenBy(t => t.Stamp.Name, StringComparer.Ordinal))
        {
            double pct = 100.0 * filled / totalVoxels;
            Console.WriteLine($"{stamp.Name,-32} {pct,6:F1}  {filled,3}/{totalVoxels}");
        }
        Console.WriteLine();

        // ASCII slice dumps for canonical shapes.
        Console.WriteLine("=== canonical shape slices ===");
        var byName = bake.Stamps.ToDictionary(s => s.Name, StringComparer.Ordinal);
        foreach (var (name, expected) in CanonicalShapes)
        {
            if (!byName.TryGetValue(name, out var stamp))
            {
                Console.WriteLine($"--- {name} (MISSING) ---");
                Console.WriteLine();
                continue;
            }
            DumpStamp(name, expected, stamp, res, totalVoxels);
            Console.WriteLine();
        }

        return 0;
    }

    // Slice layout: 8 Y-slices side-by-side, with one row per Z (top down
    // view of each Y level). Within a slice, X runs left-to-right.
    //
    //         y=0       y=1       y=2       ...
    //  z=0  ########  ########  ........
    //  z=1  ########  ########  ........
    //   ...
    private static void DumpStamp(string name, string expected, BakedStamp stamp, int res, int totalVoxels)
    {
        int filled = BakeReader.CountFilled(stamp);
        double pct = 100.0 * filled / totalVoxels;

        Console.WriteLine($"--- {name} --- fill={filled}/{totalVoxels} ({pct:F1}%) -- expect: {expected}");

        // Header: y level labels
        var header = "      ";
        for (int y = 0; y < res; y++)
        {
            header += $"y={y,-2}".PadRight(res) + " ";
        }
        Console.WriteLine(header.TrimEnd());

        for (int z = 0; z < res; z++)
        {
            var row = $"z={z,-2}  ";
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                    row += BakeReader.IsSet(stamp, res, x, y, z) ? "#" : ".";
                row += " ";
            }
            Console.WriteLine(row.TrimEnd());
        }
    }
}
