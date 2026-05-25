using System.Numerics;

namespace EmpyrionMQTT.ShapeBaker;

internal readonly record struct AABB(Vector3 Min, Vector3 Max)
{
    public static AABB Empty => new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity));

    public AABB Union(AABB other) =>
        new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    public bool IsValid => Min.X < Max.X && Min.Y < Max.Y && Min.Z < Max.Z;
}

// Voxelization via triangle-AABB rasterization + exterior flood-fill.
//
// Why not ray-parity:
//   Empyrion's shape FBXs contain stacked LOD levels per face; higher LODs
//   include beveled / inset detail with 3D thickness. A ray fired from a
//   voxel center can cross such a bevel twice, flipping the parity bit
//   inconsistently and producing an interior speckle pattern instead of a
//   solid fill.
//
// What we do instead:
//   1. For every triangle, mark each voxel its AABB overlaps as "boundary"
//      using the standard Akenine-Moller triangle-vs-AABB SAT test.
//   2. BFS-flood from every voxel on the grid's outer face, only through
//      non-boundary voxels, marking reachable voxels as "exterior".
//   3. A voxel is "filled" iff it's not exterior -- this covers both
//      surface (boundary) voxels and enclosed interior voxels.
//
// Robust to: LOD chain overlap, coincident duplicate faces, tiny mesh
// gaps smaller than a voxel, slight FP accumulation in vertex positions.
internal static class Voxelizer
{
    public static AABB ComputeAABB(DecodedMesh mesh)
    {
        var box = AABB.Empty;
        foreach (var p in mesh.Positions)
            box = new AABB(Vector3.Min(box.Min, p), Vector3.Max(box.Max, p));
        return box;
    }

    public static bool[,,] Voxelize(DecodedMesh mesh, AABB frame, int resolution)
    {
        var filled = new bool[resolution, resolution, resolution];
        if (mesh.Triangles.Length == 0 || !frame.IsValid) return filled;

        var size = frame.Max - frame.Min;
        float cellX = size.X / resolution;
        float cellY = size.Y / resolution;
        float cellZ = size.Z / resolution;
        var halfCell = new Vector3(cellX * 0.5f, cellY * 0.5f, cellZ * 0.5f);

        var boundary = new bool[resolution, resolution, resolution];
        var pos = mesh.Positions;
        var tris = mesh.Triangles;

        // Pass 1: rasterize triangles into boundary voxels.
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            var v0 = pos[tris[i]];
            var v1 = pos[tris[i + 1]];
            var v2 = pos[tris[i + 2]];

            var triMin = Vector3.Min(Vector3.Min(v0, v1), v2);
            var triMax = Vector3.Max(Vector3.Max(v0, v1), v2);

            int x0 = Math.Max(0, (int)Math.Floor((triMin.X - frame.Min.X) / cellX));
            int x1 = Math.Min(resolution - 1, (int)Math.Floor((triMax.X - frame.Min.X) / cellX));
            int y0 = Math.Max(0, (int)Math.Floor((triMin.Y - frame.Min.Y) / cellY));
            int y1 = Math.Min(resolution - 1, (int)Math.Floor((triMax.Y - frame.Min.Y) / cellY));
            int z0 = Math.Max(0, (int)Math.Floor((triMin.Z - frame.Min.Z) / cellZ));
            int z1 = Math.Min(resolution - 1, (int)Math.Floor((triMax.Z - frame.Min.Z) / cellZ));

            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                if (boundary[x, y, z]) continue;
                var center = new Vector3(
                    frame.Min.X + (x + 0.5f) * cellX,
                    frame.Min.Y + (y + 0.5f) * cellY,
                    frame.Min.Z + (z + 0.5f) * cellZ);
                if (TriBoxOverlap(center, halfCell, v0, v1, v2))
                    boundary[x, y, z] = true;
            }
        }

        // Pass 2: BFS flood from every grid-face voxel that isn't boundary.
        var exterior = new bool[resolution, resolution, resolution];
        var queue = new Queue<(int x, int y, int z)>();

        for (int x = 0; x < resolution; x++)
        for (int y = 0; y < resolution; y++)
        for (int z = 0; z < resolution; z++)
        {
            bool isFace = x == 0 || x == resolution - 1
                       || y == 0 || y == resolution - 1
                       || z == 0 || z == resolution - 1;
            if (!isFace) continue;
            if (boundary[x, y, z]) continue;
            exterior[x, y, z] = true;
            queue.Enqueue((x, y, z));
        }

        Span<(int dx, int dy, int dz)> nbrs = stackalloc (int, int, int)[]
        {
            (-1, 0, 0), (1, 0, 0),
            (0, -1, 0), (0, 1, 0),
            (0, 0, -1), (0, 0, 1),
        };

        while (queue.Count > 0)
        {
            var (cx, cy, cz) = queue.Dequeue();
            foreach (var (dx, dy, dz) in nbrs)
            {
                int nx = cx + dx, ny = cy + dy, nz = cz + dz;
                if ((uint)nx >= (uint)resolution) continue;
                if ((uint)ny >= (uint)resolution) continue;
                if ((uint)nz >= (uint)resolution) continue;
                if (boundary[nx, ny, nz]) continue;
                if (exterior[nx, ny, nz]) continue;
                exterior[nx, ny, nz] = true;
                queue.Enqueue((nx, ny, nz));
            }
        }

        // Filled = boundary OR (not exterior). Equivalently: !exterior, since
        // any voxel marked boundary is by definition not exterior.
        for (int x = 0; x < resolution; x++)
        for (int y = 0; y < resolution; y++)
        for (int z = 0; z < resolution; z++)
            filled[x, y, z] = !exterior[x, y, z];

        return filled;
    }

    // Akenine-Moller 2001 triangle-vs-AABB overlap via separating axis theorem.
    // boxCenter / boxHalf describe an axis-aligned voxel; v0/v1/v2 are the
    // triangle vertices in the same coordinate space.
    private static bool TriBoxOverlap(Vector3 boxCenter, Vector3 boxHalf,
        Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var p0 = v0 - boxCenter;
        var p1 = v1 - boxCenter;
        var p2 = v2 - boxCenter;

        // 3 AABB axes.
        if (Math.Min(p0.X, Math.Min(p1.X, p2.X)) >  boxHalf.X) return false;
        if (Math.Max(p0.X, Math.Max(p1.X, p2.X)) < -boxHalf.X) return false;
        if (Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)) >  boxHalf.Y) return false;
        if (Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)) < -boxHalf.Y) return false;
        if (Math.Min(p0.Z, Math.Min(p1.Z, p2.Z)) >  boxHalf.Z) return false;
        if (Math.Max(p0.Z, Math.Max(p1.Z, p2.Z)) < -boxHalf.Z) return false;

        var e0 = p1 - p0;
        var e1 = p2 - p1;
        var e2 = p0 - p2;

        // Triangle plane normal.
        var n = Vector3.Cross(e0, e1);
        float planeR = boxHalf.X * Math.Abs(n.X)
                     + boxHalf.Y * Math.Abs(n.Y)
                     + boxHalf.Z * Math.Abs(n.Z);
        if (Math.Abs(Vector3.Dot(n, p0)) > planeR) return false;

        // 9 edge-axis SAT tests: cross(unit-axis, triangle edge).
        if (!Sat(  0f, -e0.Z,  e0.Y, p0, p1, p2, boxHalf)) return false;
        if (!Sat(e0.Z,    0f, -e0.X, p0, p1, p2, boxHalf)) return false;
        if (!Sat(-e0.Y, e0.X,    0f, p0, p1, p2, boxHalf)) return false;

        if (!Sat(  0f, -e1.Z,  e1.Y, p0, p1, p2, boxHalf)) return false;
        if (!Sat(e1.Z,    0f, -e1.X, p0, p1, p2, boxHalf)) return false;
        if (!Sat(-e1.Y, e1.X,    0f, p0, p1, p2, boxHalf)) return false;

        if (!Sat(  0f, -e2.Z,  e2.Y, p0, p1, p2, boxHalf)) return false;
        if (!Sat(e2.Z,    0f, -e2.X, p0, p1, p2, boxHalf)) return false;
        if (!Sat(-e2.Y, e2.X,    0f, p0, p1, p2, boxHalf)) return false;

        return true;
    }

    private static bool Sat(float ax, float ay, float az,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 boxHalf)
    {
        float d0 = ax * p0.X + ay * p0.Y + az * p0.Z;
        float d1 = ax * p1.X + ay * p1.Y + az * p1.Z;
        float d2 = ax * p2.X + ay * p2.Y + az * p2.Z;
        float min = Math.Min(d0, Math.Min(d1, d2));
        float max = Math.Max(d0, Math.Max(d1, d2));
        float r = boxHalf.X * Math.Abs(ax)
                + boxHalf.Y * Math.Abs(ay)
                + boxHalf.Z * Math.Abs(az);
        return !(min > r || max < -r);
    }
}
