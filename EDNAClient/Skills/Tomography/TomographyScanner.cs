using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EDNAClient.Core;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.Tomography
{
    // Orchestrates the scan -> reconstruction pipeline:
    //   1. Player/GetProperties      -> current structure EntityId
    //   2. Structure/GetAllBlocks    -> [X, Y, Z, Type, HitPoints, Active] tabular
    //   3. Per-Type max-HP pass      -> normalize HP into a shape solidity factor in [0, 1]
    //   4. Populate DensityField     -> sparse splat into bbox + halo
    //   5. Gaussian smoothing        -> soften voxel edges
    //   6. Marching cubes            -> iso-surface mesh
    //   7. Per-vertex gradient normals
    internal class TomographyScanner
    {
        private const int   HaloVoxels   = 3;
        private const double GaussSigma  = 0.9;
        private const int   GaussRadius  = 2;
        private const float DefaultIso   = 0.25f;

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private EdnaContext? _ctx;
        private IMessageBus? _bus;

        public Task StartAsync(EdnaContext ctx)
        {
            _ctx = ctx;
            _bus = ctx.Bus;
            return Task.CompletedTask;
        }

        public void Stop() { _bus = null; _ctx = null; }

        private string TargetRcId() =>
            _ctx?.GameRcId
            ?? _ctx?.Bus.ContextRcId
            ?? throw new InvalidOperationException("TomographyScanner: no rcId available -- bus not connected");

        public async Task<TomographyDocument?> ScanAsync(
            string solarSystem, string playfield,
            Action<string> statusCallback)
        {
            if (_bus == null) throw new InvalidOperationException("TomographyScanner not started");

            try
            {
                statusCallback("Locating current structure...");
                var rcId = TargetRcId();
                var playerEnv = await _bus.RequestAsync<object>(
                    rcId, "Player", "GetProperties", new { }, RequestTimeout);

                var pj = playerEnv.PayloadJson ?? new JObject();
                var cs = pj["CurrentStructure"];
                if (cs == null || cs.Type == JTokenType.Null)
                {
                    statusCallback("Not inside a structure");
                    return null;
                }

                var entityIdToken = cs["EntityId"];
                int entityId = entityIdToken != null ? (int)entityIdToken : 0;

                statusCallback($"Scanning entity {entityId}...");
                var blocksEnv = await _bus.RequestAsync<object>(
                    rcId, "Structure", "GetAllBlocks", new { EntityId = entityId }, RequestTimeout);

                var blocksObj = JObject.Parse(blocksEnv.RawPayload);
                var blocks    = blocksObj["Blocks"] as JObject;
                if (blocks == null)
                {
                    statusCallback("No block data returned");
                    return null;
                }

                statusCallback("Reconstructing surface...");
                var doc = Build(entityId, solarSystem, playfield, blocks);
                if (doc == null) statusCallback("No blocks to scan");
                else             statusCallback($"Entity {entityId}  {doc.Indices.Length / 3} triangles");
                return doc;
            }
            catch (OperationCanceledException)
            {
                statusCallback("Scan cancelled");
                return null;
            }
            catch (Exception ex)
            {
                statusCallback($"Error: {ex.Message}");
                EdnaLogger.Error("TomographyScanner.ScanAsync failed", ex);
                return null;
            }
        }

        // Run the reconstruction pipeline against an already-fetched GetAllBlocks payload.
        // Pure (no MQTT) so it can be unit-tested or re-run on cached data later.
        public static TomographyDocument? Build(
            int entityId, string solarSystem, string playfield, JObject blocks)
        {
            var cols = blocks["Columns"] as JArray;
            var rows = blocks["Rows"]    as JArray;
            if (cols == null || rows == null || rows.Count == 0) return null;

            var colIndex = new Dictionary<string, int>();
            for (int i = 0; i < cols.Count; i++)
            {
                var name = (string?)cols[i];
                if (name != null) colIndex[name] = i;
            }
            if (!colIndex.TryGetValue("X",         out int ixX)  ||
                !colIndex.TryGetValue("Y",         out int ixY)  ||
                !colIndex.TryGetValue("Z",         out int ixZ)  ||
                !colIndex.TryGetValue("Type",      out int ixT)  ||
                !colIndex.TryGetValue("HitPoints", out int ixHp))
                return null;

            int minCols = Math.Max(Math.Max(ixX, ixY), Math.Max(Math.Max(ixZ, ixT), ixHp)) + 1;

            // Pass 1: bounding box + per-type max HP.
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            var maxHp = new Dictionary<int, int>();
            int kept = 0;

            foreach (JToken row in rows)
            {
                var arr = row as JArray;
                if (arr == null || arr.Count < minCols) continue;
                int type = (int)(arr[ixT] ?? 0);
                if (type == 0) continue;
                int hp = (int)(arr[ixHp] ?? 0);
                if (hp <= 0) continue;

                int x = (int)(arr[ixX] ?? 0);
                int y = (int)(arr[ixY] ?? 0);
                int z = (int)(arr[ixZ] ?? 0);
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;

                if (!maxHp.TryGetValue(type, out int cur) || hp > cur) maxHp[type] = hp;
                kept++;
            }
            if (kept == 0) return null;

            int W = (maxX - minX + 1) + 2 * HaloVoxels;
            int H = (maxY - minY + 1) + 2 * HaloVoxels;
            int D = (maxZ - minZ + 1) + 2 * HaloVoxels;
            var field = new DensityField(W, H, D, HaloVoxels);

            // Pass 2: splat HP / per-type-max into the field.
            foreach (JToken row in rows)
            {
                var arr = row as JArray;
                if (arr == null || arr.Count < minCols) continue;
                int type = (int)(arr[ixT] ?? 0);
                if (type == 0) continue;
                int hp = (int)(arr[ixHp] ?? 0);
                if (hp <= 0) continue;

                int x = (int)(arr[ixX] ?? 0);
                int y = (int)(arr[ixY] ?? 0);
                int z = (int)(arr[ixZ] ?? 0);

                int mx = maxHp[type];
                float density = mx > 0 ? (float)hp / mx : 1f;
                if (density > 1f) density = 1f;

                field.Splat(
                    x - minX + HaloVoxels,
                    y - minY + HaloVoxels,
                    z - minZ + HaloVoxels,
                    density);
            }

            // Smooth: separable 3D Gaussian.
            field.GaussianSmooth(DensityField.MakeGaussian(GaussSigma, GaussRadius));

            // Extract iso-surface.
            MarchingCubes.Extract(field, DefaultIso, out float[] positions, out int[] indices);
            if (positions.Length == 0 || indices.Length == 0) return null;

            // Per-vertex normals via central-difference gradient on the smoothed field.
            float[] normals = ComputeGradientNormals(field, positions);

            return TomographyDocument.FromMesh(
                entityId, solarSystem, playfield,
                minX, minY, minZ, maxX, maxY, maxZ, HaloVoxels,
                DefaultIso, positions, indices, normals);
        }

        // Central-difference gradient of the density field at each vertex,
        // negated to face outward from the solid region.
        private static float[] ComputeGradientNormals(DensityField field, float[] positions)
        {
            var normals = new float[positions.Length];
            for (int i = 0; i + 2 < positions.Length; i += 3)
            {
                float x = positions[i];
                float y = positions[i + 1];
                float z = positions[i + 2];

                float gx = field.SampleTrilinear(x + 1f, y,      z     ) - field.SampleTrilinear(x - 1f, y,      z     );
                float gy = field.SampleTrilinear(x,      y + 1f, z     ) - field.SampleTrilinear(x,      y - 1f, z     );
                float gz = field.SampleTrilinear(x,      y,      z + 1f) - field.SampleTrilinear(x,      y,      z - 1f);

                // Outward normal points opposite the gradient (density increases inward).
                float nx = -gx, ny = -gy, nz = -gz;
                float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-6f) { nx /= len; ny /= len; nz /= len; }
                else { nx = 0f; ny = 1f; nz = 0f; }

                normals[i]     = nx;
                normals[i + 1] = ny;
                normals[i + 2] = nz;
            }
            return normals;
        }
    }
}
