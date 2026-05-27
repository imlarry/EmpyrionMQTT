using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EDNAClient.Core;
using EDNAClient.Core.ShapeBake;
using ESB.Messaging;
using Newtonsoft.Json.Linq;

namespace EDNAClient.Skills.Tomography
{
    // Orchestrates the scan -> reconstruction pipeline:
    //   1. Player/GetProperties      -> current structure EntityId
    //   2. Structure/GetAllBlocks    -> [X, Y, Z, Type, HitPoints, Active, Shape, Rotation] tabular
    //   3. Per-block stamp lookup    -> ShapeStampCatalog.GetStamp(BlocksConfig.ResolveShape(...))
    //   4. Per-mode geometry emit    -> see TomographyMode
    //
    // Mode selects the geometry pipeline:
    //   VoxelCubes    -- per-block stamp + rotation, emitted as one tiny cube
    //                    per occupied voxel with intra-stamp face culling. The
    //                    only mode that renders blocks at full shape fidelity.
    //   Blocky        -- one cube per kept block with face culling, no stamps.
    //                    Coarse overview; ignores Shape.
    //   Gallery       -- synthetic: array every baked stamp in a grid so you
    //                    can see what's baked and what's missing. Hover labels.
    //   RotationAtlas -- synthetic: 24 cells, one per rotation index, of an
    //                    asymmetric marker stamp. Used to calibrate the
    //                    textbook Rotations24 table against Empyrion's actual
    //                    IBlock rotation indices.
    public enum TomographyMode { VoxelCubes, Blocky, Gallery, RotationAtlas }

    public sealed class TomographyPreset
    {
        public string         Name { get; }
        public TomographyMode Mode { get; }

        // Gallery-only: when set, BuildGallery emits only the stamps whose
        // names appear in GalleryFilter. GalleryKey disambiguates DocumentId
        // so each category opens in its own tab.
        public HashSet<string>? GalleryFilter { get; }
        public string?          GalleryKey    { get; }

        public TomographyPreset(string name, TomographyMode mode,
                                HashSet<string>? galleryFilter = null,
                                string?          galleryKey    = null)
        {
            Name          = name;
            Mode          = mode;
            GalleryFilter = galleryFilter;
            GalleryKey    = galleryKey;
        }
    }

    internal class TomographyScanner
    {
        // ── Presets ───────────────────────────────────────────────────────────
        // Sharp   : VoxelCubes -- per-block stamp + rotation emitted as tiny
        //           cubes with intra-stamp face culling. Faithful to the
        //           per-block Shape + Rotation data.
        // Blocky  : one axis-aligned unit cube per kept block with neighbor
        //           face culling. Coarse overview that ignores Shape.
        // Gallery : diagnostic. Doesn't scan -- renders every entry in
        //           ShapeStampCatalog with hover labels so you can see what's
        //           baked and spot ones that look wrong.
        // Atlas   : diagnostic. Doesn't scan -- renders 24 copies of an
        //           asymmetric marker stamp under each of our 24 rotation
        //           matrices for calibration against Empyrion's rotation
        //           indexing.
        public static readonly TomographyPreset Sharp   = new TomographyPreset("Sharp",         TomographyMode.VoxelCubes);
        public static readonly TomographyPreset Blocky  = new TomographyPreset("Blocky",        TomographyMode.Blocky);
        public static readonly TomographyPreset Gallery = new TomographyPreset("Shape Gallery", TomographyMode.Gallery);
        public static readonly TomographyPreset Atlas   = new TomographyPreset("Rotation Atlas", TomographyMode.RotationAtlas);

        public static readonly TomographyPreset[] Presets = { Sharp, Blocky, Gallery, Atlas };
        public static readonly TomographyPreset Default = Sharp;

        // Block categories dropped from reconstruction. Blocks classified into
        // one of these are skipped during the build so they render as empty air.
        // Edit via BlockClassifier.
        private static readonly HashSet<BlockCategory> SkippedCategories = new HashSet<BlockCategory>
        {
            BlockCategory.EmptySpace,
            BlockCategory.Truss,
        };

        // Per-Type skip list. Blocks whose Type is in this set are dropped
        // exactly like SkippedCategories members. Use for one-off block IDs
        // that don't warrant a whole BlockCategory entry. Populate with IDs
        // surfaced by the Sharp fallback diagnostic log.
        private static readonly HashSet<int> SkippedTypeIds = new HashSet<int>
        {
            1191, 1193, 1195, 1967,       // railings
        };

        // (Type, Shape) -> baked stamp name. Consulted BEFORE class derivation
        // and BlocksConfig.ResolveShape. Use for one-off remaps that the
        // class-driven derivation gets wrong.
        private static readonly Dictionary<(int Type, int Shape), string> StampOverrides =
            new Dictionary<(int Type, int Shape), string>
        {
        };

        // Class-driven stamp derivation for window/door/walkway blocks. Each
        // block resolves to its actual prefab stamp (baked from the models
        // bundle) via the BlocksConfig Model field. Multi-cell variants
        // (Window_v1x2, etc.) substitute their 1x1 sibling's prefab so adjacent
        // cells tile to one continuous mesh via cross-cell culling. If no
        // prefab stamp is present in the catalog the fallback is the generic
        // Wall slab (windows/doors) or CubeHalf horizontal plate (walkways).
        private static string? ResolveClassStamp(int type)
        {
            var category = BlockClassifier.Classify(type);
            if (category != BlockCategory.Window &&
                category != BlockCategory.Door &&
                category != BlockCategory.Walkway) return null;

            var def = BlocksConfig.GetById(type);
            if (def != null)
            {
                // For multi-cell variants, swap to the 1x1 sibling's Model
                // (e.g. Window_v1x2 -> Window_v1x1) so all cells of the cluster
                // stamp identical 1x1 prefab geometry and cross-cell tiling
                // can hide the seams.
                var siblingName = FindOneCellSiblingName(def.Name);
                if (siblingName != null)
                {
                    var sibDef = BlocksConfig.GetByName(siblingName);
                    if (sibDef != null && !string.IsNullOrEmpty(sibDef.Model))
                        def = sibDef;
                }

                var prefabName = ExtractPrefabName(def.Model);
                if (!string.IsNullOrEmpty(prefabName) &&
                    ShapeStampCatalog.GetStamp(prefabName) != null)
                    return prefabName;
            }

            // No prefab stamp available: generic per-class fallback.
            return category == BlockCategory.Walkway ? "CubeHalf" : "Wall";
        }

        // Replace the first NxM size token in a block name with "1x1" so the
        // caller can find the single-cell sibling block definition. Returns
        // null if the name has no such token, or the token is already 1x1.
        private static string? FindOneCellSiblingName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+x\d+");
            if (!match.Success || match.Value == "1x1") return null;
            return name.Substring(0, match.Index) + "1x1" +
                   name.Substring(match.Index + match.Length);
        }

        // Last path segment of a Model field like
        // "@models/Blocks/Windows/Standard/Window_v1x1Prefab" -> "Window_v1x1Prefab".
        private static string ExtractPrefabName(string model)
        {
            if (string.IsNullOrEmpty(model)) return "";
            int slash = model.LastIndexOf('/');
            return slash >= 0 ? model.Substring(slash + 1) : model;
        }

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        // 24 rotations in Empyrion's IBlock.Rotation index order. Calibrated
        // 2026-05-24 by placing Ramp A blocks at known rotations and reading
        // back rotation int + visual orientation against HUD world axes.
        // Encoding: rotation = facing*4 + spin. Facing in {0=+Y, 1=-Y, 2=+Z,
        // 3=-X, 4=-Z, 5=+X} is the world direction of block local +Y. Spin
        // in {0..3} is intrinsic CW-from-above yaws around block local +Y
        // with rot(N+1) = rot(N) * R_y(+90). Entries tagged V are directly
        // verified in-game; H are derived from the rule above and pending
        // visual verification via the Sharp preset on a dense test structure.
        private static readonly int[][] Rotations24 = InitRotations24();

        private EdnaContext? _ctx;
        private IMessageBus? _bus;

        private static int[][] InitRotations24()
        {
            var table = new int[][]
            {
                new[] {  1, 0, 0,    0, 1, 0,    0, 0, 1 },  // 0  V  identity
                new[] {  0, 0, 1,    0, 1, 0,   -1, 0, 0 },  // 1  V  R_y(+90)
                new[] { -1, 0, 0,    0, 1, 0,    0, 0,-1 },  // 2  V  R_y(180)
                new[] {  0, 0,-1,    0, 1, 0,    1, 0, 0 },  // 3  V  R_y(+270)
                new[] { -1, 0, 0,    0,-1, 0,    0, 0, 1 },  // 4  V  R_z(180)    facing 1 spin 0
                new[] {  0, 0,-1,    0,-1, 0,   -1, 0, 0 },  // 5  H
                new[] {  1, 0, 0,    0,-1, 0,    0, 0,-1 },  // 6  V  R_x(180)    facing 1 spin 2
                new[] {  0, 0, 1,    0,-1, 0,    1, 0, 0 },  // 7  H
                new[] { -1, 0, 0,    0, 0, 1,    0, 1, 0 },  // 8  H              facing 2 spin 0
                new[] {  0, 0,-1,   -1, 0, 0,    0, 1, 0 },  // 9  H
                new[] {  1, 0, 0,    0, 0,-1,    0, 1, 0 },  // 10 V  R_x(+90)    facing 2 spin 2
                new[] {  0, 0, 1,    1, 0, 0,    0, 1, 0 },  // 11 V              facing 2 spin 3
                new[] {  0,-1, 0,    1, 0, 0,    0, 0, 1 },  // 12 V  R_z(+90)    facing 3 spin 0
                new[] {  0,-1, 0,    0, 0, 1,   -1, 0, 0 },  // 13 H
                new[] {  0,-1, 0,   -1, 0, 0,    0, 0,-1 },  // 14 H
                new[] {  0,-1, 0,    0, 0,-1,    1, 0, 0 },  // 15 H
                new[] {  1, 0, 0,    0, 0, 1,    0,-1, 0 },  // 16 V  R_x(-90)    facing 4 spin 0
                new[] {  0, 0, 1,   -1, 0, 0,    0,-1, 0 },  // 17 H
                new[] { -1, 0, 0,    0, 0,-1,    0,-1, 0 },  // 18 H
                new[] {  0, 0,-1,    1, 0, 0,    0,-1, 0 },  // 19 H
                new[] {  0, 1, 0,   -1, 0, 0,    0, 0, 1 },  // 20 V  R_z(-90)    facing 5 spin 0
                new[] {  0, 1, 0,    0, 0,-1,   -1, 0, 0 },  // 21 H
                new[] {  0, 1, 0,    1, 0, 0,    0, 0,-1 },  // 22 H
                new[] {  0, 1, 0,    0, 0, 1,    1, 0, 0 },  // 23 H
            };

            if (table.Length != 24)
                throw new InvalidOperationException(
                    "Rotation table size mismatch: got " + table.Length + ", expected 24");
            for (int i = 0; i < 24; i++)
            {
                var m = table[i];
                if (m.Length != 9)
                    throw new InvalidOperationException(
                        "Rotation " + i + " has " + m.Length + " entries, expected 9");
                int det = m[0] * (m[4] * m[8] - m[5] * m[7])
                        - m[1] * (m[3] * m[8] - m[5] * m[6])
                        + m[2] * (m[3] * m[7] - m[4] * m[6]);
                if (det != 1)
                    throw new InvalidOperationException(
                        "Rotation " + i + " is not a proper rotation (det=" + det + ")");
            }
            return table;
        }

        // Apply rotation matrix `m` to voxel coord (sx, sy, sz) within a
        // subRes^3 stamp, returning destination voxel (rx, ry, rz). Uses
        // doubled centered coords so axis-aligned ±1 matrices produce exact
        // integer results -- no rounding, no holes.
        private static void RotateVoxel(int[] m, int sx, int sy, int sz, int subRes,
            out int rx, out int ry, out int rz)
        {
            int n = subRes - 1;
            int cx = 2 * sx - n;
            int cy = 2 * sy - n;
            int cz = 2 * sz - n;
            int xr = m[0] * cx + m[1] * cy + m[2] * cz;
            int yr = m[3] * cx + m[4] * cy + m[5] * cz;
            int zr = m[6] * cx + m[7] * cy + m[8] * cz;
            rx = (xr + n) / 2;
            ry = (yr + n) / 2;
            rz = (zr + n) / 2;
        }

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

        // Fire ESB's Structure/SetRotationAtlas on the player's current
        // structure. No document is produced -- the side effect is on the
        // in-game blocks. Pair with a Sharp / Rotation Atlas scan to compare.
        public async Task<string?> SetRotationAtlasAsync(Action<string> statusCallback)
        {
            if (_bus == null) throw new InvalidOperationException("TomographyScanner not started");
            try
            {
                statusCallback("[SetRotationAtlas] Locating current structure...");
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

                statusCallback($"[SetRotationAtlas] Writing 24 rotations to entity {entityId}...");
                var env = await _bus.RequestAsync<object>(
                    rcId, "Structure", "SetRotationAtlas",
                    new { EntityId = entityId }, RequestTimeout);

                var resp = JObject.Parse(env.RawPayload);
                var setToken = resp["Set"];
                int set = setToken != null ? (int)setToken : 0;
                var missedArr = resp["Missed"] as JArray;
                int missed = missedArr != null ? missedArr.Count : 0;
                statusCallback($"[SetRotationAtlas] Set {set}/24 rotations; {missed} cells skipped");
                if (missed > 0)
                    EdnaLogger.Log("[SetRotationAtlas] Missed cells: " + missedArr);
                return env.RawPayload;
            }
            catch (OperationCanceledException)
            {
                statusCallback("SetRotationAtlas cancelled");
                return null;
            }
            catch (Exception ex)
            {
                statusCallback($"Error: {ex.Message}");
                EdnaLogger.Error("SetRotationAtlasAsync failed", ex);
                return null;
            }
        }

        public async Task<TomographyDocument?> ScanAsync(
            string solarSystem, string playfield,
            TomographyPreset preset,
            Action<string> statusCallback)
        {
            if (_bus == null) throw new InvalidOperationException("TomographyScanner not started");
            if (preset == null) preset = Default;

            // Gallery is purely synthetic -- no bus traffic, no structure required.
            if (preset.Mode == TomographyMode.Gallery)
            {
                statusCallback($"[{preset.Name}] Building gallery from shapes.bake...");
                var filter = preset.GalleryFilter;
                var key    = preset.GalleryKey;
                var title  = preset.Name;
                var galleryDoc = await Task.Run(() => BuildGallery(filter, key, title));
                if (galleryDoc == null) statusCallback("Gallery: shapes.bake not loaded");
                else statusCallback($"[{preset.Name}] {galleryDoc.GalleryLabels?.Count ?? 0} shapes; hover for name");
                return galleryDoc;
            }

            // Rotation Atlas is also synthetic -- 24 cells of a marker stamp.
            if (preset.Mode == TomographyMode.RotationAtlas)
            {
                statusCallback($"[{preset.Name}] Building rotation atlas...");
                var atlasDoc = await Task.Run(() => BuildRotationAtlas());
                if (atlasDoc == null) statusCallback("Atlas: shape-bake resolution unavailable");
                else statusCallback($"[{preset.Name}] 24 rotations; hover for Rot index");
                return atlasDoc;
            }

            try
            {
                statusCallback($"[{preset.Name}] Locating current structure...");
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

                statusCallback($"[{preset.Name}] Scanning entity {entityId}...");
                var blocksEnv = await _bus.RequestAsync<object>(
                    rcId, "Structure", "GetAllBlocks", new { EntityId = entityId }, RequestTimeout);

                var blocksObj = JObject.Parse(blocksEnv.RawPayload);
                var blocks    = blocksObj["Blocks"] as JObject;
                if (blocks == null)
                {
                    statusCallback("No block data returned");
                    return null;
                }

                statusCallback($"[{preset.Name}] Reconstructing surface...");
                var doc = Build(entityId, solarSystem, playfield, blocks, preset);
                if (doc == null) statusCallback("No blocks to scan");
                else
                {
                    int hullTris   = doc.HullIndices.Length   / 3;
                    int windowTris = doc.WindowIndices.Length / 3;
                    statusCallback($"[{preset.Name}] Entity {entityId}  {hullTris}+{windowTris} triangles");
                }
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
            int entityId, string solarSystem, string playfield, JObject blocks, TomographyPreset? preset = null)
        {
            preset ??= Default;
            if (preset.Mode == TomographyMode.Blocky)
                return BuildBlocky(entityId, solarSystem, playfield, blocks);
            if (preset.Mode == TomographyMode.VoxelCubes)
                return BuildVoxelCubes(entityId, solarSystem, playfield, blocks);
            throw new InvalidOperationException("Build does not handle mode " + preset.Mode);
        }

        // VoxelCubes reconstruction (Sharp). Per-block stamp + rotation, emitted
        // as one tiny axis-aligned cube per occupied voxel. Face culling is
        // intra-stamp plus same-(Type,Shape,Rotation) cross-cell, so multi-cell
        // uniform surfaces (windows, hull plates) tile seamlessly. No smoothing,
        // no marching cubes; raw subRes^3 occupancy after rotation.
        private struct VoxelCell
        {
            public int Type;
            public int Shape;
            public int Rotation;
            public BakedStamp Stamp;
            public bool IsFallback;
            public BlockCategory Category;
        }

        private static TomographyDocument? BuildVoxelCubes(
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

            int ixShape    = colIndex.TryGetValue("Shape",    out int sh) ? sh : -1;
            int ixRotation = colIndex.TryGetValue("Rotation", out int rh) ? rh : -1;
            int minCols    = Math.Max(Math.Max(ixX, ixY), Math.Max(Math.Max(ixZ, ixT), ixHp)) + 1;

            if (!ShapeStampCatalog.IsLoaded)
            {
                EdnaLogger.Warn("Tomography VoxelCubes: shapes.bake not loaded");
                return null;
            }
            int subRes = ShapeStampCatalog.Resolution;
            var cubeStamp = ShapeStampCatalog.GetStamp("Cube");
            if (cubeStamp == null || subRes <= 0) return null;
            float voxSize = 1f / subRes;

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            var kept = new List<(int X, int Y, int Z, int Type, int Shape, int Rotation)>();

            foreach (JToken row in rows)
            {
                var arr = row as JArray;
                if (arr == null || arr.Count < minCols) continue;
                int type = (int)(arr[ixT] ?? 0);
                if (type == 0) continue;
                if (SkippedTypeIds.Contains(type)) continue;
                if (SkippedCategories.Contains(BlockClassifier.Classify(type))) continue;
                int hp = (int)(arr[ixHp] ?? 0);
                if (hp <= 0) continue;

                int x = (int)(arr[ixX] ?? 0);
                int y = (int)(arr[ixY] ?? 0);
                int z = (int)(arr[ixZ] ?? 0);
                int shape    = ixShape    >= 0 && arr.Count > ixShape    ? (int)(arr[ixShape]    ?? 0) : 0;
                int rotation = ixRotation >= 0 && arr.Count > ixRotation ? (int)(arr[ixRotation] ?? 0) : 0;
                if (rotation < 0 || rotation >= 24) rotation = 0;

                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;

                kept.Add((x, y, z, type, shape, rotation));
            }
            if (kept.Count == 0) return null;

            var positions      = new List<float>();
            var normals        = new List<float>();
            var indices        = new List<int>();
            var windowIndices  = new List<int>();
            var doorIndices    = new List<int>();
            var walkwayIndices = new List<int>();
            var redIdx         = new List<int>();
            var greenIdx       = new List<int>();
            var blueIdx        = new List<int>();
            var fbIdx          = new List<int>();
            var labels         = new List<TomographyLabel>();

            // Built lazily so non-calibration scans skip the cost.
            CalibrationMarker? baseMarker = null;

            // Stamp-resolution diagnostics. Track every block that fell back to
            // cubeStamp, broken down by reason and category so we can see e.g.
            // "all 42 Window blocks fell back -- (type 770, shape 0) had no
            // ChildShapes entry" vs "shape name 'WindowFull' missing from bake".
            int stampedBlocks = 0;
            int fellBackToCube = 0;
            int remapsApplied = 0;
            var fallbacksByCategory   = new Dictionary<BlockCategory, int>();
            var unresolvedTypeShapes  = new HashSet<(int Type, int Shape)>();
            var missingStamps         = new HashSet<string>();
            var remapKeysHit          = new HashSet<(int Type, int Shape)>();

            // Pass 1: resolve stamps with diagnostics and stage cells into a
            // map keyed by world coords. Rotation indicators emit immediately
            // since their colored markers are intentionally per-cell.
            var cellMap = new Dictionary<(int X, int Y, int Z), VoxelCell>(kept.Count);
            foreach (var b in kept)
            {
                float cx = b.X - minX;
                float cy = b.Y - minY;
                float cz = b.Z - minZ;

                if (IsRotationIndicator(b.Type, b.Shape))
                {
                    if (baseMarker == null) baseMarker = BuildBaseMarker(subRes);
                    var rotatedMarker = RotateMarker(baseMarker, b.Rotation, subRes);
                    EmitColoredMarker(rotatedMarker, subRes, voxSize, cx, cy, cz, yPivot: false,
                        positions, normals, indices, redIdx, greenIdx, blueIdx);

                    int intended = IntendedRotationForCell(b.X, b.Y, b.Z);
                    string note = intended < 0
                        ? "off-strip"
                        : intended == b.Rotation
                            ? "matches intended"
                            : "intended " + intended;
                    labels.Add(new TomographyLabel
                    {
                        Name        = $"world({b.X},{b.Y},{b.Z}) rot={b.Rotation} ({note})",
                        CX          = cx,
                        CY          = cy,
                        CZ          = cz,
                        Extent      = 0.55f,
                        FilledCount = 0,
                        TotalVoxels = 0,
                    });
                    continue;
                }

                var category = BlockClassifier.Classify(b.Type);
                BakedStamp? stamp = null;
                string? shapeName;
                if (StampOverrides.TryGetValue((b.Type, b.Shape), out var ovr))
                {
                    shapeName = ovr;
                    remapsApplied++;
                    remapKeysHit.Add((b.Type, b.Shape));
                }
                else
                {
                    shapeName = ResolveClassStamp(b.Type);
                    if (shapeName != null)
                    {
                        remapsApplied++;
                        remapKeysHit.Add((b.Type, b.Shape));
                    }
                    else
                    {
                        shapeName = BlocksConfig.ResolveShape(b.Type, b.Shape);
                    }
                }
                if (string.IsNullOrEmpty(shapeName))
                {
                    unresolvedTypeShapes.Add((b.Type, b.Shape));
                }
                else
                {
                    stamp = ShapeStampCatalog.GetStamp(shapeName);
                    if (stamp == null) missingStamps.Add(shapeName);
                }
                bool isFallback = stamp == null;
                if (isFallback)
                {
                    fellBackToCube++;
                    fallbacksByCategory.TryGetValue(category, out int n);
                    fallbacksByCategory[category] = n + 1;
                }
                stampedBlocks++;

                var rotated = RotateStamp(stamp ?? cubeStamp, b.Rotation, subRes);
                cellMap[(b.X, b.Y, b.Z)] = new VoxelCell
                {
                    Type       = b.Type,
                    Shape      = b.Shape,
                    Rotation   = b.Rotation,
                    Stamp      = rotated,
                    IsFallback = isFallback,
                    Category   = category,
                };
            }

            // Pass 2: emit each staged cell with cross-cell face culling. Faces
            // on a cell boundary are hidden if the adjacent cell carries the
            // same (Type, Shape, Rotation) and its mirrored-edge voxel is set.
            // Non-fallback cells route to a per-class bucket (window/door/walkway/
            // hull) so the renderer can apply a distinct material.
            foreach (var kv in cellMap)
            {
                int wx = kv.Key.X;
                int wy = kv.Key.Y;
                int wz = kv.Key.Z;
                var c = kv.Value;
                float cx = wx - minX;
                float cy = wy - minY;
                float cz = wz - minZ;
                List<int> dst;
                if (c.IsFallback)                              dst = fbIdx;
                else if (c.Category == BlockCategory.Window)   dst = windowIndices;
                else if (c.Category == BlockCategory.Door)     dst = doorIndices;
                else if (c.Category == BlockCategory.Walkway)  dst = walkwayIndices;
                else                                           dst = indices;
                EmitStampVoxelCubes(c.Stamp, subRes, voxSize, cx, cy, cz,
                    wx, wy, wz, c.Type, c.Shape, c.Rotation, cellMap,
                    positions, normals, dst);
            }

            if (fellBackToCube > 0 || remapsApplied > 0)
            {
                string byCat = fallbacksByCategory.Count == 0 ? "" :
                    " [" + string.Join(", ",
                        fallbacksByCategory
                            .OrderByDescending(kv => kv.Value)
                            .Select(kv => kv.Key + "=" + kv.Value)) + "]";
                string remap = remapsApplied == 0 ? "" :
                    ", " + remapsApplied + " remapped via override (" +
                    string.Join(", ", remapKeysHit
                        .OrderBy(p => p.Type).ThenBy(p => p.Shape)
                        .Take(20)
                        .Select(p => "(" + p.Type + "," + p.Shape + ")")) +
                    (remapKeysHit.Count > 20 ? ", ..." : "") + ")";
                string unr = unresolvedTypeShapes.Count == 0 ? "" :
                    "; unresolved (type,shape): " +
                    string.Join(", ", unresolvedTypeShapes
                        .OrderBy(p => p.Type).ThenBy(p => p.Shape)
                        .Take(20)
                        .Select(p => "(" + p.Type + "," + p.Shape + ")")) +
                    (unresolvedTypeShapes.Count > 20 ? ", ..." : "");
                string miss = missingStamps.Count == 0 ? "" :
                    "; missing stamps: " +
                    string.Join(", ", missingStamps.OrderBy(s => s, StringComparer.Ordinal).Take(20)) +
                    (missingStamps.Count > 20 ? ", ..." : "");
                EdnaLogger.Log(
                    "[Tomography] Sharp: " + stampedBlocks + " blocks" + remap +
                    ", " + fellBackToCube + " fell back to Cube" + byCat + unr + miss);
            }

            var doc = TomographyDocument.FromMesh(
                entityId, solarSystem, playfield,
                minX, minY, minZ, maxX, maxY, maxZ, 0,
                0f, positions.ToArray(),
                indices.ToArray(), windowIndices.ToArray(),
                doorIndices.ToArray(), walkwayIndices.ToArray(),
                normals.ToArray(),
                redIdx.ToArray(), greenIdx.ToArray(), blueIdx.ToArray(),
                fbIdx.ToArray());
            if (doc != null && labels.Count > 0) doc.GalleryLabels = labels;
            return doc;
        }

        // Inverse of ESB.Structure.SetRotationAtlas linear walk with defaults
        // X=0, Y=129, StartZ=2, Pitch=2, Count=24. Rotation r lives at world
        // (0, 129, 2 + r*2). Returns -1 for any other block position.
        private static int IntendedRotationForCell(int x, int y, int z)
        {
            if (x != 0 || y != 129) return -1;
            if (z < 2 || z > 48 || (z & 1) != 0) return -1;
            return (z - 2) / 2;
        }

        // Voxel cube emit with intra-stamp + same-(Type,Shape,Rotation) cross-
        // cell face culling. The six per-cell neighbour lookups happen once at
        // the top so the per-voxel inner loop only does a bool/IsSet check.
        private static void EmitStampVoxelCubes(
            BakedStamp stamp, int subRes, float voxSize,
            float cx, float cy, float cz,
            int wx, int wy, int wz,
            int matchType, int matchShape, int matchRotation,
            Dictionary<(int X, int Y, int Z), VoxelCell> cellMap,
            List<float> positions, List<float> normals, List<int> indices)
        {
            BakedStamp? nPX = null, nNX = null, nPY = null, nNY = null, nPZ = null, nNZ = null;
            if (cellMap.TryGetValue((wx + 1, wy, wz), out var cPX) && cPX.Type == matchType && cPX.Shape == matchShape && cPX.Rotation == matchRotation) nPX = cPX.Stamp;
            if (cellMap.TryGetValue((wx - 1, wy, wz), out var cNX) && cNX.Type == matchType && cNX.Shape == matchShape && cNX.Rotation == matchRotation) nNX = cNX.Stamp;
            if (cellMap.TryGetValue((wx, wy + 1, wz), out var cPY) && cPY.Type == matchType && cPY.Shape == matchShape && cPY.Rotation == matchRotation) nPY = cPY.Stamp;
            if (cellMap.TryGetValue((wx, wy - 1, wz), out var cNY) && cNY.Type == matchType && cNY.Shape == matchShape && cNY.Rotation == matchRotation) nNY = cNY.Stamp;
            if (cellMap.TryGetValue((wx, wy, wz + 1), out var cPZ) && cPZ.Type == matchType && cPZ.Shape == matchShape && cPZ.Rotation == matchRotation) nPZ = cPZ.Stamp;
            if (cellMap.TryGetValue((wx, wy, wz - 1), out var cNZ) && cNZ.Type == matchType && cNZ.Shape == matchShape && cNZ.Rotation == matchRotation) nNZ = cNZ.Stamp;

            float h = voxSize * 0.5f;
            const float half = 0.5f;
            int last = subRes - 1;
            for (int sx = 0; sx < subRes; sx++)
            for (int sy = 0; sy < subRes; sy++)
            for (int sz = 0; sz < subRes; sz++)
            {
                if (!stamp.IsSet(sx, sy, sz)) continue;
                float vx = cx + (sx + 0.5f) * voxSize - half;
                float vy = cy + (sy + 0.5f) * voxSize - half;
                float vz = cz + (sz + 0.5f) * voxSize - half;

                bool pX = sx < last ? stamp.IsSet(sx + 1, sy, sz) : (nPX != null && nPX.IsSet(0,    sy, sz));
                bool nX = sx > 0    ? stamp.IsSet(sx - 1, sy, sz) : (nNX != null && nNX.IsSet(last, sy, sz));
                bool pY = sy < last ? stamp.IsSet(sx, sy + 1, sz) : (nPY != null && nPY.IsSet(sx, 0,    sz));
                bool nY = sy > 0    ? stamp.IsSet(sx, sy - 1, sz) : (nNY != null && nNY.IsSet(sx, last, sz));
                bool pZ = sz < last ? stamp.IsSet(sx, sy, sz + 1) : (nPZ != null && nPZ.IsSet(sx, sy, 0   ));
                bool nZ = sz > 0    ? stamp.IsSet(sx, sy, sz - 1) : (nNZ != null && nNZ.IsSet(sx, sy, last));

                if (!pX) AddQuad(vx+h, vy-h, vz+h,  vx+h, vy-h, vz-h,  vx+h, vy+h, vz-h,  vx+h, vy+h, vz+h,
                                  1f, 0f, 0f, positions, normals, indices);
                if (!nX) AddQuad(vx-h, vy-h, vz-h,  vx-h, vy-h, vz+h,  vx-h, vy+h, vz+h,  vx-h, vy+h, vz-h,
                                 -1f, 0f, 0f, positions, normals, indices);
                if (!pY) AddQuad(vx-h, vy+h, vz+h,  vx+h, vy+h, vz+h,  vx+h, vy+h, vz-h,  vx-h, vy+h, vz-h,
                                  0f, 1f, 0f, positions, normals, indices);
                if (!nY) AddQuad(vx-h, vy-h, vz-h,  vx+h, vy-h, vz-h,  vx+h, vy-h, vz+h,  vx-h, vy-h, vz+h,
                                  0f, -1f, 0f, positions, normals, indices);
                if (!pZ) AddQuad(vx-h, vy-h, vz+h,  vx+h, vy-h, vz+h,  vx+h, vy+h, vz+h,  vx-h, vy+h, vz+h,
                                  0f, 0f, 1f, positions, normals, indices);
                if (!nZ) AddQuad(vx+h, vy-h, vz-h,  vx-h, vy-h, vz-h,  vx-h, vy+h, vz-h,  vx+h, vy+h, vz-h,
                                  0f, 0f, -1f, positions, normals, indices);
            }
        }

        private static TomographyDocument? BuildBlocky(
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

            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

            var kept = new List<(int X, int Y, int Z, BlockCategory Category)>();
            var occupied = new HashSet<(int X, int Y, int Z)>();

            foreach (JToken row in rows)
            {
                var arr = row as JArray;
                if (arr == null || arr.Count < minCols) continue;
                int type = (int)(arr[ixT] ?? 0);
                if (type == 0) continue;
                if (SkippedTypeIds.Contains(type)) continue;
                var category = BlockClassifier.Classify(type);
                if (SkippedCategories.Contains(category)) continue;
                int hp = (int)(arr[ixHp] ?? 0);
                if (hp <= 0) continue;

                int x = (int)(arr[ixX] ?? 0);
                int y = (int)(arr[ixY] ?? 0);
                int z = (int)(arr[ixZ] ?? 0);

                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;

                kept.Add((x, y, z, category));
                occupied.Add((x, y, z));
            }
            if (kept.Count == 0) return null;

            var positions      = new List<float>();
            var normals        = new List<float>();
            var hullIndices    = new List<int>();
            var windowIndices  = new List<int>();
            var doorIndices    = new List<int>();
            var walkwayIndices = new List<int>();

            foreach (var b in kept)
            {
                List<int> dst;
                if      (b.Category == BlockCategory.Window)  dst = windowIndices;
                else if (b.Category == BlockCategory.Door)    dst = doorIndices;
                else if (b.Category == BlockCategory.Walkway) dst = walkwayIndices;
                else                                          dst = hullIndices;
                EmitVisibleFaces(b.X, b.Y, b.Z, minX, minY, minZ, occupied, positions, normals, dst);
            }

            return TomographyDocument.FromMesh(
                entityId, solarSystem, playfield,
                minX, minY, minZ, maxX, maxY, maxZ, 0,
                0f, positions.ToArray(),
                hullIndices.ToArray(), windowIndices.ToArray(),
                doorIndices.ToArray(), walkwayIndices.ToArray(),
                normals.ToArray());
        }

        // Lay out every baked stamp in a sqrt(N) x sqrt(N) grid, emitting one
        // tiny axis-aligned cube per occupied voxel with same-stamp neighbor
        // culling. Records per-shape hover-pick labels in the document so the
        // panel can name what the mouse points at.
        //
        // Cell convention matches the bake: X,Z span [-0.5, +0.5], Y spans
        // [0, 1] around each grid origin (gx*pitch, 0, gz*pitch). Cells are
        // 2 block-units apart so the gap reads visually as "negative space"
        // around each shape.
        public static TomographyDocument? BuildGallery() =>
            BuildGallery(null, null, "Shape Gallery");

        public static TomographyDocument? BuildGallery(
            HashSet<string>? filter, string? galleryKey, string title)
        {
            if (!ShapeStampCatalog.IsLoaded)
            {
                EdnaLogger.Warn("Tomography Gallery: shapes.bake not loaded");
                return null;
            }
            int subRes = ShapeStampCatalog.Resolution;
            if (subRes <= 0) return null;

            IEnumerable<BakedStamp> source = ShapeStampCatalog.All;
            if (filter != null) source = source.Where(s => filter.Contains(s.Name));
            var stamps = source.OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
            if (stamps.Count == 0) return null;

            const float CellPitch = 2f;          // 1-unit shape + 1-unit gap
            float voxSize = 1f / subRes;

            int cols = (int)Math.Ceiling(Math.Sqrt(stamps.Count));
            int rows = (stamps.Count + cols - 1) / cols;

            var positions = new List<float>(stamps.Count * 64 * 24);
            var normals   = new List<float>(stamps.Count * 64 * 24);
            var indices   = new List<int>  (stamps.Count * 64 * 36);
            var labels    = new List<TomographyLabel>(stamps.Count);

            int totalVoxels = subRes * subRes * subRes;

            for (int i = 0; i < stamps.Count; i++)
            {
                int gx = i % cols;
                int gz = i / cols;
                float ox = gx * CellPitch;   // cell center X
                float oz = gz * CellPitch;   // cell center Z
                EmitStampCubes(stamps[i], subRes, voxSize, ox, oz, positions, normals, indices);

                labels.Add(new TomographyLabel
                {
                    Name        = stamps[i].Name,
                    CX          = ox,
                    CY          = 0.5f,
                    CZ          = oz,
                    Extent      = 0.55f,
                    FilledCount = stamps[i].FilledCount,
                    TotalVoxels = totalVoxels,
                });
            }

            // Bbox spans cell-center positions in block units. The Center math
            // ((Max-Min)/2 + Halo) lands the camera on the grid midpoint;
            // Diagonal adds +1 per axis so the actual shape extents (which
            // overhang each cell by 0.5) still fit in the framing.
            int minX = 0, maxX = (int)Math.Round((cols - 1) * CellPitch);
            int minY = 0, maxY = 1;
            int minZ = 0, maxZ = (int)Math.Round((rows - 1) * CellPitch);

            var doc = TomographyDocument.FromGallery(
                minX, minY, minZ, maxX, maxY, maxZ,
                positions.ToArray(), indices.ToArray(), normals.ToArray(),
                labels);
            doc.GalleryKey   = galleryKey;
            doc.GalleryTitle = title;
            return doc;
        }

        // Build a linear atlas that mirrors the in-game calibration rig:
        //   - cement core block at (0, 0, 0)
        //   - 24 rotation markers along +Z on the y=1 plane at (0, 1, 2..48 step 2)
        //   - 24 pairs of cement companion blocks on the y=0 plane at
        //     (1, 0, 2..48 step 2) and (2, 0, 2..48 step 2)
        // Rotation r is the marker at z = 2 + r*2 (closest to core = rot 0).
        // Tip colors: red=+Z arm, green=+X arm, blue=+Y arm.
        public static TomographyDocument? BuildRotationAtlas()
        {
            if (!ShapeStampCatalog.IsLoaded) return null;
            int subRes = ShapeStampCatalog.Resolution;
            if (subRes <= 0) return null;

            var baseMarker = BuildBaseMarker(subRes);
            float voxSize = 1f / subRes;

            var positions = new List<float>();
            var normals   = new List<float>();
            var hullIdx   = new List<int>();
            var redIdx    = new List<int>();
            var greenIdx  = new List<int>();
            var blueIdx   = new List<int>();
            var labels    = new List<TomographyLabel>(24);
            int total = subRes * subRes * subRes;

            // Cement reference: core at origin + two parallel 2-block companions.
            EmitUnitCube(0f, 0.5f, 0f, positions, normals, hullIdx);
            for (int rot = 0; rot < 24; rot++)
            {
                float z = 2f + rot * 2f;
                EmitUnitCube(1f, 0.5f, z, positions, normals, hullIdx);
                EmitUnitCube(2f, 0.5f, z, positions, normals, hullIdx);
            }

            for (int rot = 0; rot < 24; rot++)
            {
                float ox = 0f;
                float oy = 1.5f;
                float oz = 2f + rot * 2f;

                var rotated = RotateMarker(baseMarker, rot, subRes);
                EmitColoredMarker(rotated, subRes, voxSize, ox, oy, oz, yPivot: false,
                    positions, normals, hullIdx, redIdx, greenIdx, blueIdx);

                labels.Add(new TomographyLabel
                {
                    Name        = $"Rot {rot}  facing {rot / 4} spin {rot % 4}  cell(X=0,Z={oz:0})",
                    CX          = ox,
                    CY          = oy,
                    CZ          = oz,
                    Extent      = 0.55f,
                    FilledCount = rotated.Stamp.FilledCount,
                    TotalVoxels = total,
                });
            }

            int minX = 0, maxX = 2;
            int minY = 0, maxY = 2;
            int minZ = 0, maxZ = 48;

            return TomographyDocument.FromGallery(
                minX, minY, minZ, maxX, maxY, maxZ,
                positions.ToArray(), hullIdx.ToArray(), normals.ToArray(),
                labels,
                redIdx.ToArray(), greenIdx.ToArray(), blueIdx.ToArray());
        }

        // Emit a unit (1x1x1) axis-aligned cube centered at (cx, cy, cz).
        // All six faces go to the hull index list; no neighbor culling.
        private static void EmitUnitCube(
            float cx, float cy, float cz,
            List<float> positions, List<float> normals, List<int> indices)
        {
            const float h = 0.5f;
            AddQuad(cx+h, cy-h, cz+h,  cx+h, cy-h, cz-h,  cx+h, cy+h, cz-h,  cx+h, cy+h, cz+h,  1f, 0f, 0f, positions, normals, indices);
            AddQuad(cx-h, cy-h, cz-h,  cx-h, cy-h, cz+h,  cx-h, cy+h, cz+h,  cx-h, cy+h, cz-h, -1f, 0f, 0f, positions, normals, indices);
            AddQuad(cx-h, cy+h, cz+h,  cx+h, cy+h, cz+h,  cx+h, cy+h, cz-h,  cx-h, cy+h, cz-h,  0f, 1f, 0f, positions, normals, indices);
            AddQuad(cx-h, cy-h, cz-h,  cx+h, cy-h, cz-h,  cx+h, cy-h, cz+h,  cx-h, cy-h, cz+h,  0f,-1f, 0f, positions, normals, indices);
            AddQuad(cx-h, cy-h, cz+h,  cx+h, cy-h, cz+h,  cx+h, cy+h, cz+h,  cx-h, cy+h, cz+h,  0f, 0f, 1f, positions, normals, indices);
            AddQuad(cx+h, cy-h, cz-h,  cx-h, cy-h, cz-h,  cx-h, cy+h, cz-h,  cx+h, cy+h, cz-h,  0f, 0f,-1f, positions, normals, indices);
        }

        // Indicator block selector: the in-game asymmetric "rotation
        // indicator" block used to calibrate the Rotations24 table against
        // Empyrion's IBlock.Rotation index. Hard-coded for now; the
        // calibration model places 24 of these in a 5x5 grid (one corner
        // empty) and the Sharp preset replaces their geometry with the
        // colored marker so the in-game scan matches the synthetic atlas.
        private static bool IsRotationIndicator(int type, int shape) =>
            type == 1840 && shape == 26;

        // Arm-voxel descriptor: a voxel within the calibration marker whose
        // visible faces should render in a tip color rather than the default
        // hull material. Color: 1=red, 2=green, 3=blue.
        private struct TipFace
        {
            public int Sx, Sy, Sz;
            public int ColorIdx;
        }

        // Asymmetric marker stamp plus its three RGB tip faces. Used by
        // both the synthetic RotationAtlas and the Sharp preset's
        // substitution path for in-game indicator blocks.
        private sealed class CalibrationMarker
        {
            public readonly BakedStamp Stamp;
            public readonly List<TipFace> Tips;
            public CalibrationMarker(BakedStamp stamp, List<TipFace> tips)
            {
                Stamp = stamp; Tips = tips;
            }
        }

        // Identity-orientation marker: every voxel of each arm is colored.
        //   +X arm -> green (X axis)
        //   +Y arm -> blue  (Y axis)
        //   +Z arm -> red   (Z axis)
        // Origin (0,0,0) is left as hull material so the join point reads
        // as a neutral reference.
        private static CalibrationMarker BuildBaseMarker(int subRes)
        {
            var stamp = BuildCalibrationStamp(subRes);
            var tips = new List<TipFace>();
            for (int i = 1; i < subRes;     i++) tips.Add(new TipFace { Sx = i, Sy = 0, Sz = 0, ColorIdx = 2 });
            for (int i = 1; i < subRes - 1; i++) tips.Add(new TipFace { Sx = 0, Sy = i, Sz = 0, ColorIdx = 3 });
            for (int i = 1; i < subRes - 2; i++) tips.Add(new TipFace { Sx = 0, Sy = 0, Sz = i, ColorIdx = 1 });
            return new CalibrationMarker(stamp, tips);
        }

        // Apply Rotations24[rotation] to the marker stamp and each arm voxel.
        private static CalibrationMarker RotateMarker(CalibrationMarker src, int rotation, int subRes)
        {
            var m = Rotations24[rotation];
            var stamp = RotateStamp(src.Stamp, rotation, subRes);
            var tips = new List<TipFace>(src.Tips.Count);
            foreach (var t in src.Tips)
            {
                RotateVoxel(m, t.Sx, t.Sy, t.Sz, subRes, out int rx, out int ry, out int rz);
                tips.Add(new TipFace { Sx = rx, Sy = ry, Sz = rz, ColorIdx = t.ColorIdx });
            }
            return new CalibrationMarker(stamp, tips);
        }

        // Emit each occupied voxel of a (rotated) calibration marker, sending
        // faces matching a tip entry into the matching color index buffer and
        // the remaining visible (uncovered by same-stamp neighbor) faces into
        // hullIndices. yPivot=true means Y origin lives at the base of the
        // stamp (used by the synthetic Atlas, which sits on the ground plane);
        // yPivot=false means the stamp is centered on (ox, oy, oz) (used by
        // Sharp, which renders blocks at their world Y).
        private static void EmitColoredMarker(
            CalibrationMarker marker, int subRes, float voxSize,
            float ox, float oy, float oz, bool yPivot,
            List<float> positions, List<float> normals,
            List<int> hullIndices, List<int> redIndices, List<int> greenIndices, List<int> blueIndices)
        {
            var stamp = marker.Stamp;
            var tips  = marker.Tips;
            float h = voxSize * 0.5f;
            const float half = 0.5f;

            for (int sx = 0; sx < subRes; sx++)
            for (int sy = 0; sy < subRes; sy++)
            for (int sz = 0; sz < subRes; sz++)
            {
                if (!stamp.IsSet(sx, sy, sz)) continue;

                float cx = ox + (sx + 0.5f) * voxSize - half;
                float cy = yPivot ? (sy + 0.5f) * voxSize
                                  : oy + (sy + 0.5f) * voxSize - half;
                float cz = oz + (sz + 0.5f) * voxSize - half;

                EmitColoredFace(stamp, tips, sx, sy, sz,  1, 0, 0, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
                EmitColoredFace(stamp, tips, sx, sy, sz, -1, 0, 0, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
                EmitColoredFace(stamp, tips, sx, sy, sz,  0, 1, 0, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
                EmitColoredFace(stamp, tips, sx, sy, sz,  0,-1, 0, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
                EmitColoredFace(stamp, tips, sx, sy, sz,  0, 0, 1, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
                EmitColoredFace(stamp, tips, sx, sy, sz,  0, 0,-1, subRes, cx, cy, cz, h,
                                positions, normals, hullIndices, redIndices, greenIndices, blueIndices);
            }
        }

        private static void EmitColoredFace(
            BakedStamp stamp, List<TipFace> tips,
            int sx, int sy, int sz, int nx, int ny, int nz, int subRes,
            float cx, float cy, float cz, float h,
            List<float> positions, List<float> normals,
            List<int> hullIndices, List<int> redIndices, List<int> greenIndices, List<int> blueIndices)
        {
            // Intra-stamp face culling: a face whose neighbor in (nx, ny, nz)
            // is also occupied is hidden. Internal arm-to-arm and arm-to-origin
            // faces are culled here; the remaining visible faces of an arm
            // voxel all take the arm color.
            int ax = sx + nx, ay = sy + ny, az = sz + nz;
            if (ax >= 0 && ax < subRes && ay >= 0 && ay < subRes && az >= 0 && az < subRes &&
                stamp.IsSet(ax, ay, az))
                return;

            List<int> dst = hullIndices;
            for (int i = 0; i < tips.Count; i++)
            {
                var t = tips[i];
                if (t.Sx == sx && t.Sy == sy && t.Sz == sz)
                {
                    if      (t.ColorIdx == 1) dst = redIndices;
                    else if (t.ColorIdx == 2) dst = greenIndices;
                    else if (t.ColorIdx == 3) dst = blueIndices;
                    break;
                }
            }

            if      (nx ==  1) AddQuad(cx+h, cy-h, cz+h,  cx+h, cy-h, cz-h,  cx+h, cy+h, cz-h,  cx+h, cy+h, cz+h,  1f, 0f, 0f, positions, normals, dst);
            else if (nx == -1) AddQuad(cx-h, cy-h, cz-h,  cx-h, cy-h, cz+h,  cx-h, cy+h, cz+h,  cx-h, cy+h, cz-h, -1f, 0f, 0f, positions, normals, dst);
            else if (ny ==  1) AddQuad(cx-h, cy+h, cz+h,  cx+h, cy+h, cz+h,  cx+h, cy+h, cz-h,  cx-h, cy+h, cz-h,  0f, 1f, 0f, positions, normals, dst);
            else if (ny == -1) AddQuad(cx-h, cy-h, cz-h,  cx+h, cy-h, cz-h,  cx+h, cy-h, cz+h,  cx-h, cy-h, cz+h,  0f,-1f, 0f, positions, normals, dst);
            else if (nz ==  1) AddQuad(cx-h, cy-h, cz+h,  cx+h, cy-h, cz+h,  cx+h, cy+h, cz+h,  cx-h, cy+h, cz+h,  0f, 0f, 1f, positions, normals, dst);
            else if (nz == -1) AddQuad(cx+h, cy-h, cz-h,  cx-h, cy-h, cz-h,  cx-h, cy+h, cz-h,  cx+h, cy+h, cz-h,  0f, 0f,-1f, positions, normals, dst);
        }

        // Asymmetric calibration stamp: an origin voxel at (0,0,0) plus three
        // perpendicular arms with distinct lengths along +X (4 vox), +Y (3),
        // +Z (2). Every one of the 24 rotations produces a visually distinct
        // 3D L-shape, so the user can identify the orientation produced by
        // each index unambiguously.
        private static BakedStamp BuildCalibrationStamp(int subRes)
        {
            int total = subRes * subRes * subRes;
            var occ = new bool[total];
            int filled = 0;

            void Set(int x, int y, int z)
            {
                if (x < 0 || x >= subRes || y < 0 || y >= subRes || z < 0 || z >= subRes) return;
                int idx = x * subRes * subRes + y * subRes + z;
                if (!occ[idx]) { occ[idx] = true; filled++; }
            }

            Set(0, 0, 0);
            for (int i = 1; i < subRes;     i++) Set(i, 0, 0);   // +X arm (length subRes)
            for (int i = 1; i < subRes - 1; i++) Set(0, i, 0);   // +Y arm (length subRes-1)
            for (int i = 1; i < subRes - 2; i++) Set(0, 0, i);   // +Z arm (length subRes-2)

            return new BakedStamp("Marker", subRes, occ, filled);
        }

        // Produce a new stamp whose occupancy is the source stamp rotated by
        // the given rotation index. Used by BuildRotationAtlas to pre-rotate
        // each cell's voxel grid before reusing EmitStampCubes for rendering.
        private static BakedStamp RotateStamp(BakedStamp source, int rotation, int subRes)
        {
            var mat = Rotations24[rotation];
            int total = subRes * subRes * subRes;
            var occ = new bool[total];
            int filled = 0;

            for (int sx = 0; sx < subRes; sx++)
            for (int sy = 0; sy < subRes; sy++)
            for (int sz = 0; sz < subRes; sz++)
            {
                if (!source.IsSet(sx, sy, sz)) continue;
                RotateVoxel(mat, sx, sy, sz, subRes, out int dx, out int dy, out int dz);
                int idx = dx * subRes * subRes + dy * subRes + dz;
                if (!occ[idx]) { occ[idx] = true; filled++; }
            }

            return new BakedStamp(source.Name + "_R" + rotation, subRes, occ, filled);
        }

        // Emit each occupied voxel as a small cube; cull faces shared with an
        // occupied same-stamp neighbor. Voxel (sx, sy, sz) sits at world
        //   X = ox + (sx + 0.5)/subRes - 0.5
        //   Y =      (sy + 0.5)/subRes            (bake Y pivot is at base)
        //   Z = oz + (sz + 0.5)/subRes - 0.5
        private static void EmitStampCubes(
            BakedStamp stamp, int subRes, float voxSize,
            float ox, float oz,
            List<float> positions, List<float> normals, List<int> indices)
        {
            float h = voxSize * 0.5f;
            float half = 0.5f;
            for (int sx = 0; sx < subRes; sx++)
            for (int sy = 0; sy < subRes; sy++)
            for (int sz = 0; sz < subRes; sz++)
            {
                if (!stamp.IsSet(sx, sy, sz)) continue;

                float cx = ox + (sx + 0.5f) * voxSize - half;
                float cy =      (sy + 0.5f) * voxSize;
                float cz = oz + (sz + 0.5f) * voxSize - half;

                bool pX = sx + 1 < subRes && stamp.IsSet(sx + 1, sy, sz);
                bool nX = sx - 1 >= 0     && stamp.IsSet(sx - 1, sy, sz);
                bool pY = sy + 1 < subRes && stamp.IsSet(sx, sy + 1, sz);
                bool nY = sy - 1 >= 0     && stamp.IsSet(sx, sy - 1, sz);
                bool pZ = sz + 1 < subRes && stamp.IsSet(sx, sy, sz + 1);
                bool nZ = sz - 1 >= 0     && stamp.IsSet(sx, sy, sz - 1);

                if (!pX) AddQuad(cx+h, cy-h, cz+h,  cx+h, cy-h, cz-h,  cx+h, cy+h, cz-h,  cx+h, cy+h, cz+h,
                                  1f,  0f,  0f, positions, normals, indices);
                if (!nX) AddQuad(cx-h, cy-h, cz-h,  cx-h, cy-h, cz+h,  cx-h, cy+h, cz+h,  cx-h, cy+h, cz-h,
                                 -1f,  0f,  0f, positions, normals, indices);
                if (!pY) AddQuad(cx-h, cy+h, cz+h,  cx+h, cy+h, cz+h,  cx+h, cy+h, cz-h,  cx-h, cy+h, cz-h,
                                  0f,  1f,  0f, positions, normals, indices);
                if (!nY) AddQuad(cx-h, cy-h, cz-h,  cx+h, cy-h, cz-h,  cx+h, cy-h, cz+h,  cx-h, cy-h, cz+h,
                                  0f, -1f,  0f, positions, normals, indices);
                if (!pZ) AddQuad(cx-h, cy-h, cz+h,  cx+h, cy-h, cz+h,  cx+h, cy+h, cz+h,  cx-h, cy+h, cz+h,
                                  0f,  0f,  1f, positions, normals, indices);
                if (!nZ) AddQuad(cx+h, cy-h, cz-h,  cx-h, cy-h, cz-h,  cx-h, cy+h, cz-h,  cx+h, cy+h, cz-h,
                                  0f,  0f, -1f, positions, normals, indices);
            }
        }

        // Emit the visible (not-culled) faces of a unit cube centered on the
        // block's local position. Each face contributes 4 fresh vertices with
        // the face normal -- flat shading -- and two CCW-wound triangles.
        private static void EmitVisibleFaces(
            int wx, int wy, int wz,
            int minX, int minY, int minZ,
            HashSet<(int X, int Y, int Z)> occupied,
            List<float> positions, List<float> normals, List<int> indices)
        {
            float cx = wx - minX;
            float cy = wy - minY;
            float cz = wz - minZ;
            const float h = 0.5f;

            if (!occupied.Contains((wx + 1, wy, wz)))
                AddQuad(cx+h, cy-h, cz+h,  cx+h, cy-h, cz-h,  cx+h, cy+h, cz-h,  cx+h, cy+h, cz+h,
                         1f,  0f,  0f, positions, normals, indices);
            if (!occupied.Contains((wx - 1, wy, wz)))
                AddQuad(cx-h, cy-h, cz-h,  cx-h, cy-h, cz+h,  cx-h, cy+h, cz+h,  cx-h, cy+h, cz-h,
                        -1f,  0f,  0f, positions, normals, indices);
            if (!occupied.Contains((wx, wy + 1, wz)))
                AddQuad(cx-h, cy+h, cz+h,  cx+h, cy+h, cz+h,  cx+h, cy+h, cz-h,  cx-h, cy+h, cz-h,
                         0f,  1f,  0f, positions, normals, indices);
            if (!occupied.Contains((wx, wy - 1, wz)))
                AddQuad(cx-h, cy-h, cz-h,  cx+h, cy-h, cz-h,  cx+h, cy-h, cz+h,  cx-h, cy-h, cz+h,
                         0f, -1f,  0f, positions, normals, indices);
            if (!occupied.Contains((wx, wy, wz + 1)))
                AddQuad(cx-h, cy-h, cz+h,  cx+h, cy-h, cz+h,  cx+h, cy+h, cz+h,  cx-h, cy+h, cz+h,
                         0f,  0f,  1f, positions, normals, indices);
            if (!occupied.Contains((wx, wy, wz - 1)))
                AddQuad(cx+h, cy-h, cz-h,  cx-h, cy-h, cz-h,  cx-h, cy+h, cz-h,  cx+h, cy+h, cz-h,
                         0f,  0f, -1f, positions, normals, indices);
        }

        private static void AddQuad(
            float x0, float y0, float z0,
            float x1, float y1, float z1,
            float x2, float y2, float z2,
            float x3, float y3, float z3,
            float nx, float ny, float nz,
            List<float> positions, List<float> normals, List<int> indices)
        {
            int baseIdx = positions.Count / 3;
            positions.Add(x0); positions.Add(y0); positions.Add(z0);
            positions.Add(x1); positions.Add(y1); positions.Add(z1);
            positions.Add(x2); positions.Add(y2); positions.Add(z2);
            positions.Add(x3); positions.Add(y3); positions.Add(z3);
            for (int i = 0; i < 4; i++) { normals.Add(nx); normals.Add(ny); normals.Add(nz); }
            indices.Add(baseIdx);     indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
            indices.Add(baseIdx);     indices.Add(baseIdx + 2); indices.Add(baseIdx + 3);
        }

    }
}
