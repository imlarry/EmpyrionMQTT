using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EDNAClient.Core;
using Newtonsoft.Json;

namespace EDNAClient.Skills.Tomography
{
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

        // Flat arrays for compact JSON. Positions/Normals are xyz triples; Indices is i0,i1,i2 triples.
        public float[] Positions { get; set; } = Array.Empty<float>();
        public int[]   Indices   { get; set; } = Array.Empty<int>();
        public float[] Normals   { get; set; } = Array.Empty<float>();

        // ── Runtime-only ──────────────────────────────────────────────────────

        [JsonIgnore] public string DocumentId => $"tomo-{EntityId}";
        [JsonIgnore] public string ShortTitle => $"Entity {EntityId}";

        [JsonIgnore] private MeshGeometry3D? _mesh;
        [JsonIgnore] public MeshGeometry3D? Mesh
        {
            get => _mesh;
            private set { _mesh = value; OnPropertyChanged(); }
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
            float iso, float[] positions, int[] indices, float[] normals)
        {
            var doc = new TomographyDocument
            {
                EntityId    = entityId,
                Playfield   = playfield,
                SolarSystem = solarSystem,
                CapturedAt  = DateTime.UtcNow.ToString("o"),
                MinX = minX, MinY = minY, MinZ = minZ,
                MaxX = maxX, MaxY = maxY, MaxZ = maxZ,
                Halo = halo,
                Iso  = iso,
                Positions = positions,
                Indices   = indices,
                Normals   = normals,
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
            Positions = source.Positions;
            Indices   = source.Indices;
            Normals   = source.Normals;
            RebuildMesh();
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

        // Convert flat arrays into a WPF MeshGeometry3D. Mesh is centered on the
        // structure's bounding box so a default camera at (0, 0, dist) frames it.
        public void RebuildMesh()
        {
            var mesh = new MeshGeometry3D();
            if (Positions == null || Positions.Length < 3 || Indices == null || Indices.Length < 3)
            {
                Mesh = mesh;
                return;
            }

            double cx = Center.X, cy = Center.Y, cz = Center.Z;

            var points = new Point3DCollection(Positions.Length / 3);
            for (int i = 0; i + 2 < Positions.Length; i += 3)
                points.Add(new Point3D(Positions[i] - cx, Positions[i + 1] - cy, Positions[i + 2] - cz));
            mesh.Positions = points;

            var ints = new Int32Collection(Indices.Length);
            for (int i = 0; i < Indices.Length; i++) ints.Add(Indices[i]);
            mesh.TriangleIndices = ints;

            if (Normals != null && Normals.Length == Positions.Length)
            {
                var nrm = new Vector3DCollection(Normals.Length / 3);
                for (int i = 0; i + 2 < Normals.Length; i += 3)
                    nrm.Add(new Vector3D(Normals[i], Normals[i + 1], Normals[i + 2]));
                mesh.Normals = nrm;
            }

            mesh.Freeze();
            Mesh = mesh;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
