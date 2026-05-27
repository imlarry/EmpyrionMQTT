using System.Diagnostics;
using System.Numerics;
using EmpyrionMQTT.ShapeBaker;

const string DefaultBundle =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Bundles\shapes";
const string DefaultModelsBundle =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Bundles\models";
const string DefaultShapesEcf =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Configuration\BlockShapesWindow.ecf";
const string DefaultBlocksConfig =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Configuration\BlocksConfig.ecf";

string bundlePath        = DefaultBundle;
string modelsBundlePath  = DefaultModelsBundle;
string shapesEcfPath     = DefaultShapesEcf;
string blocksConfigPath  = DefaultBlocksConfig;
string outputPath        = "shapes.bake";
string? tpkPath          = null;
string? verifyPath       = null;
int  resolution          = 8;
bool useGlobalFrame      = false;
bool noFilter            = false;
bool skipModels          = false;
string? listRootsBundle  = null;
string? listRootsFilter  = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--bundle":         bundlePath        = args[++i]; break;
        case "--models-bundle":  modelsBundlePath  = args[++i]; break;
        case "--shapes":         shapesEcfPath     = args[++i]; break;
        case "--blocks-config":  blocksConfigPath  = args[++i]; break;
        case "--output":         outputPath        = args[++i]; break;
        case "--resolution":     resolution        = int.Parse(args[++i]); break;
        case "--tpk":            tpkPath           = args[++i]; break;
        case "--global-frame":   useGlobalFrame    = true; break;
        case "--no-filter":      noFilter          = true; break;
        case "--skip-models":    skipModels        = true; break;
        case "--verify":         verifyPath        = args[++i]; break;
        case "--list-bundle-roots":
            // Form: --list-bundle-roots <shapes|models> [substring]
            listRootsBundle = args[++i];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                listRootsFilter = args[++i];
            break;
        case "-h":
        case "--help":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"unknown arg: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (verifyPath != null)
    return VerifyMode.Run(verifyPath);

if (listRootsBundle != null)
    return ListBundleRoots(listRootsBundle, listRootsFilter, bundlePath, modelsBundlePath, tpkPath);

if (resolution < 2 || resolution > 64)
{
    Console.Error.WriteLine($"resolution out of range: {resolution} (allowed 2..64)");
    return 1;
}
if (!File.Exists(bundlePath))
{
    Console.Error.WriteLine($"bundle not found: {bundlePath}");
    return 1;
}
if (!noFilter && !File.Exists(shapesEcfPath))
{
    Console.Error.WriteLine($"BlockShapesWindow.ecf not found: {shapesEcfPath}");
    return 1;
}
if (!skipModels)
{
    if (!File.Exists(modelsBundlePath))
    {
        Console.Error.WriteLine($"models bundle not found: {modelsBundlePath}");
        return 1;
    }
    if (!File.Exists(blocksConfigPath))
    {
        Console.Error.WriteLine($"BlocksConfig.ecf not found: {blocksConfigPath}");
        return 1;
    }
}

HashSet<string>? wantedShapeNames = null;
if (!noFilter)
{
    wantedShapeNames = EcfShapeReader.ReadShapeNames(shapesEcfPath);
    Console.Error.WriteLine(
        $"shape name set: {wantedShapeNames.Count} names from {Path.GetFileName(shapesEcfPath)}");
}
else
{
    Console.Error.WriteLine(
        "shape name filter: DISABLED (--no-filter); every FBX root shape in the bundle will be baked");
}

var sw = Stopwatch.StartNew();

// Read the shapes bundle (primitives like Cube, Wall, CornerRound, etc.).
// Shape primitive FBXs use bottom-pivot Y (Y=0 at cell floor, Y=1 at top),
// matching our default voxelization frame, so no position offset.
Console.Error.WriteLine($"reading shapes bundle: {bundlePath}");
var shapesDecoded = ReadAndDecode(bundlePath, tpkPath, wantedShapeNames, "shapes", Vector3.Zero);

// Read the models bundle for window/door/walkway prefabs. The filter is
// BlocksConfig.ecf's set of 1x1 (single-cell) Window/Door/Walkway/Hangar
// prefab names; multi-cell blocks reuse their 1x1 sibling's stamp via
// cross-cell tiling at render time, so we don't bake them.
//
// Model-bundle prefabs use cell-CENTERED Y (Y=-0.5 at cell floor, Y=+0.5
// at top) -- different from shape primitives' bottom-pivot. We apply a
// +0.5 Y offset so the prefab geometry lands inside our [0,1] Y frame.
var modelsDecoded = new List<DecodedMesh>();
if (!skipModels)
{
    var prefabNames = BlocksConfigPrefabReader.ReadPrefabNames(blocksConfigPath);
    Console.Error.WriteLine(
        $"prefab name set: {prefabNames.Count} window/door/walkway 1x1 prefabs from {Path.GetFileName(blocksConfigPath)}");
    Console.Error.WriteLine($"reading models bundle: {modelsBundlePath}");
    modelsDecoded = ReadAndDecode(modelsBundlePath, tpkPath, prefabNames, "models",
        new Vector3(0f, 0.5f, 0f));
}

// Combine: shape primitives first, then model prefabs. Stamp names from the
// two sources don't collide -- prefab names always end in "Prefab".
var decoded = shapesDecoded.Concat(modelsDecoded).ToList();

if (decoded.Count == 0)
{
    Console.Error.WriteLine(
        "no meshes matched -- verify the bundle paths and that the filter sets contain names that exist in the bundles.");
    return 2;
}

// Submesh-level diagnostic TSV. One row per collected MeshFilter across
// both bundles so we can spot LOD chains, mis-placed faces, or shapes
// whose collected geometry sits outside the unit cell.
var tsvPath = outputPath + ".submeshes.tsv";
using (var tsv = new StreamWriter(tsvPath))
{
    tsv.WriteLine("shape\towner\tverts\ttris\tmin_x\tmin_y\tmin_z\tmax_x\tmax_y\tmax_z");
    foreach (var m in decoded.OrderBy(d => d.Name, StringComparer.Ordinal))
    {
        var bb = Voxelizer.ComputeAABB(m);
        tsv.WriteLine(
            $"{m.Name}\t{m.OwnerName}\t{m.Positions.Length}\t{m.Triangles.Length / 3}\t" +
            $"{bb.Min.X:F4}\t{bb.Min.Y:F4}\t{bb.Min.Z:F4}\t" +
            $"{bb.Max.X:F4}\t{bb.Max.Y:F4}\t{bb.Max.Z:F4}");
    }
}
Console.Error.WriteLine($"wrote {tsvPath} (submesh-level diagnostic)");

// Diagnostic: log the Cube mesh AABB so we can sanity-check the cell
// convention. For Empyrion shape FBXs we expect Cube to span +-0.5 in
// every axis (cell center at origin, edges at +-0.5).
var cubeMesh = decoded.FirstOrDefault(m => m.Name == "Cube");
if (cubeMesh != null)
{
    var cb = Voxelizer.ComputeAABB(cubeMesh);
    Console.Error.WriteLine(
        $"Cube mesh AABB: min=({cb.Min.X:F3},{cb.Min.Y:F3},{cb.Min.Z:F3}) "
        + $"max=({cb.Max.X:F3},{cb.Max.Y:F3},{cb.Max.Z:F3})");
}

// Diagnostic: log a sample prefab mesh AABB to confirm the prefab pivot
// matches the shape-primitive convention. If prefabs use a different pivot
// (e.g. cell-center Y instead of bottom Y), this is where we'll see it.
var samplePrefab = decoded.FirstOrDefault(m => m.Name == "Window_v1x1Prefab");
if (samplePrefab != null)
{
    var pb = Voxelizer.ComputeAABB(samplePrefab);
    Console.Error.WriteLine(
        $"Window_v1x1Prefab AABB: min=({pb.Min.X:F3},{pb.Min.Y:F3},{pb.Min.Z:F3}) "
        + $"max=({pb.Max.X:F3},{pb.Max.Y:F3},{pb.Max.Z:F3})");
}

// Stamp reference frame. Default: fixed unit cell using Empyrion's
// authoring convention -- pivot at the bottom-center of the cell, so X
// and Z span +-0.5 around the origin but Y spans 0..1. A Cube fills the
// whole grid; multi-cell shapes (corridor pillars) get truncated to
// their first cell. --global-frame uses the union of every mesh's AABB.
AABB frame;
if (useGlobalFrame)
{
    frame = AABB.Empty;
    foreach (var m in decoded) frame = frame.Union(Voxelizer.ComputeAABB(m));
}
else
{
    frame = new AABB(new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 1f, 0.5f));
}

Console.Error.WriteLine(
    $"decoded {decoded.Count} meshes ({shapesDecoded.Count} shapes + {modelsDecoded.Count} prefabs) "
    + $"in {sw.ElapsedMilliseconds} ms; frame ({(useGlobalFrame ? "global" : "unit-cube")}) "
    + $"min=({frame.Min.X:F3},{frame.Min.Y:F3},{frame.Min.Z:F3}) "
    + $"max=({frame.Max.X:F3},{frame.Max.Y:F3},{frame.Max.Z:F3})");

sw.Restart();
var stamps = new (string Name, bool[,,] Stamp)[decoded.Count];
Parallel.For(0, decoded.Count, i =>
{
    var m = decoded[i];
    stamps[i] = (m.Name, Voxelizer.Voxelize(m, frame, resolution));
});
Console.Error.WriteLine($"voxelized at {resolution}^3 in {sw.ElapsedMilliseconds} ms");

BakeWriter.Write(outputPath, resolution, frame, stamps);
var info = new FileInfo(outputPath);
Console.Error.WriteLine($"wrote {info.FullName} ({info.Length} bytes)");

return 0;


// Load all meshes from a bundle whose root GameObject name appears in `filter`
// (or every root if filter is null), decode each MeshFilter sub-asset, merge
// per-shape submeshes, and return the merged DecodedMesh list ordered by name.
// `positionOffset` is applied to every decoded mesh position so bundles with
// a different pivot convention (e.g. models bundle's cell-centered Y) align
// with the voxelization frame.
static List<DecodedMesh> ReadAndDecode(
    string bundlePath, string? tpkPath, HashSet<string>? filter, string label,
    Vector3 positionOffset)
{
    var partsByName = new Dictionary<string, List<DecodedMesh>>(StringComparer.Ordinal);
    int seen = 0, decodeFails = 0;
    var failNames = new List<string>();

    using (var reader = new BundleReader(bundlePath, tpkPath))
    {
        foreach (var raw in reader.EnumerateMeshes(filter))
        {
            seen++;

            DecodedMesh? mesh;
            try { mesh = MeshDecoder.Decode(raw); }
            catch (Exception ex)
            {
                decodeFails++;
                if (failNames.Count < 10) failNames.Add($"{raw.Name} ({ex.GetType().Name})");
                continue;
            }
            if (mesh == null)
            {
                decodeFails++;
                if (failNames.Count < 10) failNames.Add(raw.Name);
                continue;
            }

            if (positionOffset != Vector3.Zero)
            {
                for (int i = 0; i < mesh.Positions.Length; i++)
                    mesh.Positions[i] += positionOffset;
            }

            if (!partsByName.TryGetValue(raw.Name, out var list))
                partsByName[raw.Name] = list = new List<DecodedMesh>();
            list.Add(mesh);
        }
    }

    Console.Error.WriteLine(
        $"{label}: submeshes seen={seen} shapes={partsByName.Count} decode-failed={decodeFails}");
    if (failNames.Count > 0)
        Console.Error.WriteLine($"  first decode-failed: " + string.Join(", ", failNames));

    if (filter != null)
    {
        var missing = filter.Where(n => !partsByName.ContainsKey(n)).OrderBy(n => n).ToList();
        if (missing.Count > 0)
            Console.Error.WriteLine(
                $"  {label}: unresolved names ({missing.Count}): " +
                string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? ", ..." : ""));
    }

    return partsByName
        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => MergeSubmeshes(kv.Key, kv.Value))
        .ToList();
}

// Merge per-face submeshes into a single mesh per shape. World matrices
// are already baked into each submesh's positions by MeshDecoder, so this
// is a flat concatenation with index re-offsetting.
static DecodedMesh MergeSubmeshes(string name, List<DecodedMesh> parts)
{
    int totalV = parts.Sum(p => p.Positions.Length);
    int totalT = parts.Sum(p => p.Triangles.Length);
    var positions = new Vector3[totalV];
    var triangles = new int[totalT];
    int vOff = 0, tOff = 0;
    foreach (var p in parts)
    {
        Array.Copy(p.Positions, 0, positions, vOff, p.Positions.Length);
        for (int i = 0; i < p.Triangles.Length; i++)
            triangles[tOff + i] = p.Triangles[i] + vOff;
        vOff += p.Positions.Length;
        tOff += p.Triangles.Length;
    }
    return new DecodedMesh { Name = name, Positions = positions, Triangles = triangles };
}

// Diagnostic: print every root GameObject name in the chosen bundle to
// stdout, one per line. Filter is an optional case-insensitive substring
// applied to the name. Used to confirm what's actually present in a bundle
// when a filter set produces fewer baked stamps than expected.
static int ListBundleRoots(string which, string? filter,
                           string shapesBundle, string modelsBundle, string? tpkPath)
{
    string path;
    switch (which)
    {
        case "shapes": path = shapesBundle; break;
        case "models": path = modelsBundle; break;
        default:
            Console.Error.WriteLine($"--list-bundle-roots: unknown bundle '{which}' (expected shapes|models)");
            return 1;
    }
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"--list-bundle-roots: bundle not found: {path}");
        return 1;
    }

    // Count occurrences so callers can spot prefabs whose hierarchy contains
    // many same-named children (e.g. DoorDouble's repeated Doorframe nodes)
    // separately from genuine root prefabs.
    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
    using (var reader = new BundleReader(path, tpkPath))
    {
        foreach (var n in reader.EnumerateRootNames())
            counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1;
    }

    int totalGOs = 0;
    foreach (var kv in counts) totalGOs += kv.Value;
    Console.Error.WriteLine(
        $"--list-bundle-roots {which}: {totalGOs} GameObjects ({counts.Count} unique names) in {path}");

    IEnumerable<KeyValuePair<string, int>> filtered = counts;
    if (filter != null)
    {
        filtered = counts.Where(kv =>
            kv.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        var matched = filtered.Count();
        Console.Error.WriteLine($"--list-bundle-roots filter '{filter}': {matched} unique matches");
    }

    foreach (var kv in filtered.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        Console.WriteLine(kv.Value > 1 ? $"{kv.Key}  (x{kv.Value})" : kv.Key);

    return 0;
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        "usage: ShapeBaker [--bundle <p>] [--models-bundle <p>] [--shapes <p>] [--blocks-config <p>]");
    Console.Error.WriteLine(
        "                  [--output <p>] [--resolution <n>] [--tpk <p>] [--no-filter] [--skip-models]");
    Console.Error.WriteLine(
        "  --bundle         path to Empyrion's Content/Bundles/shapes file");
    Console.Error.WriteLine(
        "  --models-bundle  path to Empyrion's Content/Bundles/models file (window/door/walkway prefabs)");
    Console.Error.WriteLine(
        "  --shapes         path to Empyrion's Content/Configuration/BlockShapesWindow.ecf");
    Console.Error.WriteLine(
        "  --blocks-config  path to Empyrion's Content/Configuration/BlocksConfig.ecf");
    Console.Error.WriteLine(
        "  --output         output bake path (default: shapes.bake)");
    Console.Error.WriteLine(
        "  --resolution     voxel cube side length, 2..64 (default: 8)");
    Console.Error.WriteLine(
        "  --tpk            optional classdata.tpk if a bundle lacks type-tree info");
    Console.Error.WriteLine(
        "  --global-frame   use the union of all mesh AABBs as the stamp frame");
    Console.Error.WriteLine(
        "                   (default: unit cell with bottom-pivot Y -- X,Z spans +-0.5, Y spans 0..1)");
    Console.Error.WriteLine(
        "  --no-filter      ignore BlockShapesWindow.ecf and bake every FBX root shape in the shapes bundle");
    Console.Error.WriteLine(
        "  --skip-models    skip the models bundle pass; produces a shapes-only bake");
    Console.Error.WriteLine(
        "  --verify <p>     read the bake at <p> and print analysis instead of producing one");
    Console.Error.WriteLine(
        "  --list-bundle-roots <which> [substring]");
    Console.Error.WriteLine(
        "                   diagnostic: dump every root GameObject name in the chosen bundle");
    Console.Error.WriteLine(
        "                   <which>: 'shapes' or 'models' -- the corresponding bundle path is used");
    Console.Error.WriteLine(
        "                   [substring]: optional case-insensitive name filter");
}
