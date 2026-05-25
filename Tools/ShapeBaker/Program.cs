using System.Diagnostics;
using System.Numerics;
using EmpyrionMQTT.ShapeBaker;

const string DefaultBundle =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Bundles\shapes";
const string DefaultShapesEcf =
    @"C:\Program Files (x86)\Steam\steamapps\common\Empyrion - Galactic Survival\Content\Configuration\BlockShapesWindow.ecf";

string bundlePath = DefaultBundle;
string shapesEcfPath = DefaultShapesEcf;
string outputPath = "shapes.bake";
string? tpkPath = null;
string? verifyPath = null;
int resolution = 8;
bool useGlobalFrame = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--bundle":       bundlePath    = args[++i]; break;
        case "--shapes":       shapesEcfPath = args[++i]; break;
        case "--output":       outputPath    = args[++i]; break;
        case "--resolution":   resolution    = int.Parse(args[++i]); break;
        case "--tpk":          tpkPath       = args[++i]; break;
        case "--global-frame": useGlobalFrame = true; break;
        case "--verify":       verifyPath    = args[++i]; break;
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
if (!File.Exists(shapesEcfPath))
{
    Console.Error.WriteLine($"BlockShapesWindow.ecf not found: {shapesEcfPath}");
    return 1;
}

var wantedNames = EcfShapeReader.ReadShapeNames(shapesEcfPath);
Console.Error.WriteLine($"shape name set: {wantedNames.Count} names from {Path.GetFileName(shapesEcfPath)}");

Console.Error.WriteLine($"reading bundle: {bundlePath}");
var sw = Stopwatch.StartNew();

// Pass 1: enumerate every submesh in the bundle for each in-filter
// shape. A single FBX is usually broken into several face GameObjects,
// each with its own MeshFilter and a world transform that places the
// face on the right side of the shape. We collect them all per name,
// then merge into a single mesh for voxelization.
var partsByName = new Dictionary<string, List<DecodedMesh>>(StringComparer.Ordinal);
int seen = 0, decodeFails = 0;
var failNames = new List<string>();

using (var reader = new BundleReader(bundlePath, tpkPath))
{
    foreach (var raw in reader.EnumerateMeshes(wantedNames))
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

        if (!partsByName.TryGetValue(raw.Name, out var list))
            partsByName[raw.Name] = list = new List<DecodedMesh>();
        list.Add(mesh);
    }
}

Console.Error.WriteLine(
    $"submeshes seen={seen} shapes={partsByName.Count} decode-failed={decodeFails}");
if (failNames.Count > 0)
    Console.Error.WriteLine("first decode-failed: " + string.Join(", ", failNames));

var missing = wantedNames.Where(n => !partsByName.ContainsKey(n)).OrderBy(n => n).ToList();
if (missing.Count > 0)
    Console.Error.WriteLine(
        $"shape names in ecf with no matching bundle mesh ({missing.Count}): " +
        string.Join(", ", missing.Take(20)) + (missing.Count > 20 ? ", ..." : ""));

if (partsByName.Count == 0)
{
    Console.Error.WriteLine("no shape meshes matched -- "
        + "verify the bundle path and that BlockShapesWindow.ecf shape names exist in it.");
    return 2;
}

// Write a submesh-level diagnostic TSV alongside the bake. One row per
// collected MeshFilter so we can spot LOD chains, mis-placed faces,
// or shapes whose collected geometry sits outside the unit cell.
var tsvPath = outputPath + ".submeshes.tsv";
using (var tsv = new StreamWriter(tsvPath))
{
    tsv.WriteLine("shape\towner\tverts\ttris\tmin_x\tmin_y\tmin_z\tmax_x\tmax_y\tmax_z");
    foreach (var (shape, parts) in partsByName.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
        foreach (var p in parts)
        {
            var bb = Voxelizer.ComputeAABB(p);
            tsv.WriteLine(
                $"{shape}\t{p.OwnerName}\t{p.Positions.Length}\t{p.Triangles.Length / 3}\t" +
                $"{bb.Min.X:F4}\t{bb.Min.Y:F4}\t{bb.Min.Z:F4}\t" +
                $"{bb.Max.X:F4}\t{bb.Max.Y:F4}\t{bb.Max.Z:F4}");
        }
    }
}
Console.Error.WriteLine($"wrote {tsvPath} (submesh-level diagnostic)");

var decoded = partsByName
    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
    .Select(kv => MergeSubmeshes(kv.Key, kv.Value))
    .ToList();

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
    $"decoded {decoded.Count} meshes in {sw.ElapsedMilliseconds} ms; "
    + $"frame ({(useGlobalFrame ? "global" : "unit-cube")}) "
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

static void PrintUsage()
{
    Console.Error.WriteLine(
        "usage: ShapeBaker [--bundle <path>] [--shapes <path>] [--output <path>] [--resolution <n>] [--tpk <path>]");
    Console.Error.WriteLine(
        "  --bundle      path to Empyrion's Content/Bundles/shapes file");
    Console.Error.WriteLine(
        "  --shapes      path to Empyrion's Content/Configuration/BlockShapesWindow.ecf");
    Console.Error.WriteLine(
        "  --output      output bake path (default: shapes.bake)");
    Console.Error.WriteLine(
        "  --resolution  voxel cube side length, 2..64 (default: 8)");
    Console.Error.WriteLine(
        "  --tpk           optional classdata.tpk if the bundle lacks type-tree info");
    Console.Error.WriteLine(
        "  --global-frame  use the union of all mesh AABBs as the stamp frame");
    Console.Error.WriteLine(
        "                  (default: unit cell with bottom-pivot Y -- X,Z spans +-0.5, Y spans 0..1)");
    Console.Error.WriteLine(
        "  --verify <p>    read the bake at <p> and print analysis instead of producing one");
}
