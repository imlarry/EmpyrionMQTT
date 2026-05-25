using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EDNAClient.Core;
using Newtonsoft.Json;

namespace EDNAClient.Skills.Tomography
{
    // One hover-pick target in a Shape Gallery document. Position is in the
    // same absolute coord space as TomographyDocument.Positions, i.e. BEFORE
    // the Center subtraction RebuildMesh applies; the panel applies the same
    // offset when ray-testing so picks match the rendered geometry.
    public sealed class TomographyLabel
    {
        public string Name        { get; set; } = "";
        public float  CX          { get; set; }
        public float  CY          { get; set; }
        public float  CZ          { get; set; }
        public float  Extent      { get; set; }
        public int    FilledCount { get; set; }
        public int    TotalVoxels { get; set; }
    }

    // One captured structure tomogram. Persisted properties hold the mesh as
    // flat float/int arrays; the runtime MeshGeometry3D is rebuilt from those
    // on Load/UpdateFrom so the document tab can bind directly.
    public sealed class TomographyDocument : INotifyPropertyChanged
    {
        // ── Persisted ─────────────────────────────────────────────────────────

        public int    EntityId    { get; set; }
        public string Playfield   { get; set; } = "";
        public string SolarSystem { get; set; } = "";
        public string CapturedAt  { get; set; } = "";

        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MinZ { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int MaxZ { get; set; }
        public int Halo { get; set; }

        public float Iso { get; set; } = 0.25f;

        // Flat arrays for compact JSON. Positions/Normals are xyz triples;
        // HullIndices/WindowIndices are i0,i1,i2 triples into Positions.
        // Indices is the legacy single-list field retained so saves written
        // before the hull/window split still load -- migrated to HullIndices
        // on first read.
        public float[] Positions     { get; set; } = Array.Empty<float>();
        public int[]   Indices       { get; set; } = Array.Empty<int>();
        public int[]   HullIndices   { get; set; } = Array.Empty<int>();
        public int[]   WindowIndices { get; set; } = Array.Empty<int>();
        public float[] Normals       { get; set; } = Array.Empty<float>();

        // Tip-color index buffers for the RotationAtlas / Sharp calibration
        // marker. Each is a list of triangle indices into Positions, just like
        // HullIndices, but rendered with a solid R / G / B material so the user
        // can read off the marker's per-axis orientation under each rotation.
        // R = +Z arm tip, G = +X arm tip, B = +Y arm tip (matches the in-game
        // indicator block's face coloring).
        public int[] RedTipIndices   { get; set; } = Array.Empty<int>();
        public int[] GreenTipIndices { get; set; } = Array.Empty<int>();
        public int[] BlueTipIndices  { get; set; } = Array.Empty<int>();

        // Shape-gallery hover labels. Populated only by BuildGallery; persisted so
        // a re-opened gallery still picks. Positions are in the same coord space
        // as Positions (pre-Center subtraction); the panel applies the offset.
        public List<TomographyLabel>? GalleryLabels { get; set; }

        // ── Runtime-only ──────────────────────────────────────────────────────

        [JsonIgnore] public bool IsGallery =>
            GalleryLabels != null && GalleryLabels.Count > 0;

        [JsonIgnore] public string DocumentId =>
            IsGallery ? "tomo-gallery" : $"tomo-{EntityId}";

        [JsonIgnore] public string ShortTitle =>
            IsGallery ? "Shape Gallery" : $"Entity {EntityId}";

        [JsonIgnore] private MeshGeometry3D? _hullMesh;
        [JsonIgnore] public MeshGeometry3D? HullMesh
        {
            get => _hullMesh;
            private set { _hullMesh = value; OnPropertyChanged(); }
        }

        [JsonIgnore] private MeshGeometry3D? _windowMesh;
        [JsonIgnore] public MeshGeometry3D? WindowMesh
        {
            get => _windowMesh;
            private set { _windowMesh = value; OnPropertyChanged(); }
        }

        [JsonIgnore] private MeshGeometry3D? _redTipMesh;
        [JsonIgnore] public MeshGeometry3D? RedTipMesh
        {
            get => _redTipMesh;
            private set { _redTipMesh = value; OnPropertyChanged(); }
        }

        [JsonIgnore] private MeshGeometry3D? _greenTipMesh;
        [JsonIgnore] public MeshGeometry3D? GreenTipMesh
        {
            get => _greenTipMesh;
            private set { _greenTipMesh = value; OnPropertyChanged(); }
        }

        [JsonIgnore] private MeshGeometry3D? _blueTipMesh;
        [JsonIgnore] public MeshGeometry3D? BlueTipMesh
        {
            get => _blueTipMesh;
            private set { _blueTipMesh = value; OnPropertyChanged(); }
        }

        // Center of the structure's bounding box in field-local coords (post-halo).
        [JsonIgnore]
        public Point3D Center => new Point3D(
            (MaxX - MinX) / 2.0 + Halo,
            (MaxY - MinY) / 2.0 + Halo,
            (MaxZ - MinZ) / 2.0 + Halo);

        // Diagonal of the bounding box, used to size the camera.
        [JsonIgnore]
        public double Diagonal
        {
            get
            {
                double dx = MaxX - MinX + 1;
                double dy = MaxY - MinY + 1;
                double dz = MaxZ - MinZ + 1;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        [JsonIgnore] private string _statusText = "";
        [JsonIgnore] public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        // ── Factory ───────────────────────────────────────────────────────────

        public static TomographyDocument FromMesh(
            int entityId, string solarSystem, string playfield,
            int minX, int minY, int minZ, int maxX, int maxY, int maxZ, int halo,
            float iso, float[] positions, int[] hullIndices, int[] windowIndices, float[] normals)
        {
            return FromMesh(entityId, solarSystem, playfield,
                minX, minY, minZ, maxX, maxY, maxZ, halo,
                iso, positions, hullIndices, windowIndices, normals,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
        }

        public static TomographyDocument FromMesh(
            int entityId, string solarSystem, string playfield,
            int minX, int minY, int minZ, int maxX, int maxY, int maxZ, int halo,
            float iso, float[] positions, int[] hullIndices, int[] windowIndices, float[] normals,
            int[] redTipIndices, int[] greenTipIndices, int[] blueTipIndices)
        {
            var doc = new TomographyDocument
            {
                EntityId      = entityId,
                Playfield     = playfield,
                SolarSystem   = solarSystem,
                CapturedAt    = DateTime.UtcNow.ToString("o"),
                MinX = minX, MinY = minY, MinZ = minZ,
                MaxX = maxX, MaxY = maxY, MaxZ = maxZ,
                Halo = halo,
                Iso  = iso,
                Positions       = positions,
                HullIndices     = hullIndices,
                WindowIndices   = windowIndices,
                Normals         = normals,
                RedTipIndices   = redTipIndices,
                GreenTipIndices = greenTipIndices,
                BlueTipIndices  = blueTipIndices,
            };
            doc.RebuildMesh();
            return doc;
        }

        // Copies all data from source and rebuilds the runtime mesh.
        public void UpdateFrom(TomographyDocument source)
        {
            EntityId    = source.EntityId;
            Playfield   = source.Playfield;
            SolarSystem = source.SolarSystem;
            CapturedAt  = source.CapturedAt;
            MinX = source.MinX; MinY = source.MinY; MinZ = source.MinZ;
            MaxX = source.MaxX; MaxY = source.MaxY; MaxZ = source.MaxZ;
            Halo = source.Halo;
            Iso  = source.Iso;
            Positions       = source.Positions;
            Indices         = source.Indices;
            HullIndices     = source.HullIndices;
            WindowIndices   = source.WindowIndices;
            Normals         = source.Normals;
            RedTipIndices   = source.RedTipIndices;
            GreenTipIndices = source.GreenTipIndices;
            BlueTipIndices  = source.BlueTipIndices;
            GalleryLabels   = source.GalleryLabels;
            RebuildMesh();
        }

        // Shape-gallery variant of FromMesh. Uses a sentinel EntityId (-1) and
        // a single layer in Y so the bbox math produces a sensible camera frame.
        public static TomographyDocument FromGallery(
            int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
            float[] positions, int[] hullIndices, float[] normals,
            List<TomographyLabel> labels)
        {
            return FromGallery(minX, minY, minZ, maxX, maxY, maxZ,
                positions, hullIndices, normals, labels,
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
        }

        public static TomographyDocument FromGallery(
            int minX, int minY, int minZ, int maxX, int maxY, int maxZ,
            float[] positions, int[] hullIndices, float[] normals,
            List<TomographyLabel> labels,
            int[] redTipIndices, int[] greenTipIndices, int[] blueTipIndices)
        {
            var doc = new TomographyDocument
            {
                EntityId      = -1,
                Playfield     = "ShapeBake",
                SolarSystem   = "Gallery",
                CapturedAt    = DateTime.UtcNow.ToString("o"),
                MinX = minX, MinY = minY, MinZ = minZ,
                MaxX = maxX, MaxY = maxY, MaxZ = maxZ,
                Halo = 0,
                Iso  = 0f,
                Positions       = positions,
                HullIndices     = hullIndices,
                WindowIndices   = Array.Empty<int>(),
                Normals         = normals,
                RedTipIndices   = redTipIndices,
                GreenTipIndices = greenTipIndices,
                BlueTipIndices  = blueTipIndices,
                GalleryLabels   = labels,
            };
            doc.RebuildMesh();
            return doc;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public static TomographyDocument? Load(string path)
        {
            try
            {
                var doc = JsonConvert.DeserializeObject<TomographyDocument>(File.ReadAllText(path));
                doc?.RebuildMesh();
                return doc;
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"TomographyDocument.Load failed ({path}): {ex.Message}");
                return null;
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        // ── Mesh build ────────────────────────────────────────────────────────

        // Convert flat arrays into two WPF MeshGeometry3D's -- one for hull
        // triangles and one for window triangles. Both share the same Positions
        // and Normals collections (frozen, so safe to share); only the
        // TriangleIndices differ. Mesh is centered on the structure's bounding
        // box so a default camera at (0, 0, dist) frames it.
        //
        // Empyrion (Unity) is left-handed; WPF Viewport3D is right-handed. We
        // convert at this single boundary by negating Z on positions and
        // normals and swapping the last two indices of every triangle so the
        // winding order stays CCW relative to the (now-flipped) outward normal.
        public void RebuildMesh()
        {
            // One-time migration: pre-window-split saves wrote everything to
            // Indices. Move it to HullIndices and clear the legacy field.
            if (HullIndices.Length == 0 && WindowIndices.Length == 0 && Indices.Length > 0)
            {
                HullIndices = Indices;
                Indices = Array.Empty<int>();
            }

            if (Positions == null || Positions.Length < 3 ||
                (HullIndices.Length < 3 && WindowIndices.Length < 3 &&
                 RedTipIndices.Length < 3 && GreenTipIndices.Length < 3 && BlueTipIndices.Length < 3))
            {
                HullMesh     = new MeshGeometry3D();
                WindowMesh   = null;
                RedTipMesh   = null;
                GreenTipMesh = null;
                BlueTipMesh  = null;
                return;
            }

            double cx = Center.X, cy = Center.Y, cz = Center.Z;

            var points = new Point3DCollection(Positions.Length / 3);
            for (int i = 0; i + 2 < Positions.Length; i += 3)
                points.Add(new Point3D(Positions[i] - cx, Positions[i + 1] - cy, -(Positions[i + 2] - cz)));
            points.Freeze();

            Vector3DCollection? normals = null;
            if (Normals != null && Normals.Length == Positions.Length)
            {
                normals = new Vector3DCollection(Normals.Length / 3);
                for (int i = 0; i + 2 < Normals.Length; i += 3)
                    normals.Add(new Vector3D(Normals[i], Normals[i + 1], -Normals[i + 2]));
                normals.Freeze();
            }

            HullMesh   = BuildSubMesh(points, normals, HullIndices);
            WindowMesh = WindowIndices.Length >= 3
                ? BuildSubMesh(points, normals, WindowIndices)
                : null;
            RedTipMesh   = RedTipIndices.Length   >= 3 ? BuildSubMesh(points, normals, RedTipIndices)   : null;
            GreenTipMesh = GreenTipIndices.Length >= 3 ? BuildSubMesh(points, normals, GreenTipIndices) : null;
            BlueTipMesh  = BlueTipIndices.Length  >= 3 ? BuildSubMesh(points, normals, BlueTipIndices)  : null;
        }

        // Triangle winding is reversed (i0, i2, i1) to compensate for the Z
        // flip applied to positions in RebuildMesh; without this, every face
        // would point inward and back-face cull / light from the wrong side.
        private static MeshGeometry3D BuildSubMesh(
            Point3DCollection points, Vector3DCollection? normals, int[] indices)
        {
            var mesh = new MeshGeometry3D { Positions = points };
            if (normals != null) mesh.Normals = normals;

            var ints = new Int32Collection(indices.Length);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                ints.Add(indices[i]);
                ints.Add(indices[i + 2]);
                ints.Add(indices[i + 1]);
            }
            mesh.TriangleIndices = ints;

            mesh.Freeze();
            return mesh;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
