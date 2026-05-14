using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EDNAClient.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Color         = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using Point         = System.Windows.Point;

namespace EDNAClient.Skills.FloorMap
{
    // Stored per block in the JSON file.  Includes grid position so the
    // dict can be rebuilt from the list on load without extra metadata.
    public sealed class BlockEntry
    {
        public int X        { get; set; }
        public int Z        { get; set; }
        public int Type     { get; set; }
        public int Shape    { get; set; }
        public int Rotation { get; set; }
    }

    // One captured structure: raw GetAllBlocks data + metadata + rendered bitmap.
    // Blocks is the GetAllBlocks response table: { Columns: [...], Rows: [[...], ...] }.
    // Render() filters to Y (walls) and Y-1 (floors) from the rows.
    public sealed class FloorMapDocument : INotifyPropertyChanged
    {
        // ── Tile rendering constants ──────────────────────────────────────────

        private const int TileSize   = 32;
        private const int LabelTop   = 20;
        private const int LabelRight = 52;

        private static readonly Brush WallBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)));
        private static readonly Brush FloorBrush     = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)));
        private static readonly Brush WallFloorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x68, 0x68, 0x68)));
        private static readonly Brush WalkwayBrush   = Freeze(new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)));
        private static readonly Brush PlayerBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0xCC)));
        private static readonly System.Windows.Media.Pen GridPen = FreezePen(new System.Windows.Media.Pen(Brushes.White, 1.0));
        private static readonly Brush    LabelBrush  = Brushes.White;
        private static readonly Typeface LabelFace   = new Typeface("Consolas");
        private const double LabelFontSize = 8.5;

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }
        private static System.Windows.Media.Pen FreezePen(System.Windows.Media.Pen p) { p.Freeze(); return p; }

        private enum TileCategory { None, Door, Console, Walkway }

        private static readonly Dictionary<int, TileCategory> TypeCategory =
            new Dictionary<int, TileCategory>
            {
                [1817] = TileCategory.Door,    [1886] = TileCategory.Door,
                [1816] = TileCategory.Door,    [2014] = TileCategory.Door,
                [258]  = TileCategory.Console, [261]  = TileCategory.Console,
                [1243] = TileCategory.Console, [635]  = TileCategory.Console,
                [727]  = TileCategory.Console, [1469] = TileCategory.Console,
                [1087] = TileCategory.Console, [636]  = TileCategory.Console,
                [884]  = TileCategory.Walkway, [972]  = TileCategory.Walkway,
            };

        // Block type IDs invisible in-game; excluded from the rendered map.
        private static readonly HashSet<int> SkippedTypes = new HashSet<int>();

        // ── Persisted properties ──────────────────────────────────────────────

        public int    EntityId    { get; set; }
        public int    Y           { get; set; }
        public int    PlayerX     { get; set; }
        public int    PlayerZ     { get; set; }
        public string Playfield   { get; set; } = "";
        public string SolarSystem { get; set; } = "";
        public string CapturedAt  { get; set; } = "";

        // GetAllBlocks tabular response: { Columns: [...], Rows: [[...], ...] }.
        public JObject Blocks { get; set; } = new JObject();

        // ── Runtime-only properties ───────────────────────────────────────────

        [JsonIgnore] public string DocumentId => EntityId.ToString();
        [JsonIgnore] public string ShortTitle => $"Entity {EntityId}";

        [JsonIgnore] private BitmapSource? _mapImage;
        [JsonIgnore] public BitmapSource? MapImage
        {
            get => _mapImage;
            private set { _mapImage = value; OnPropertyChanged(); }
        }

        [JsonIgnore] private string _statusText = "";
        [JsonIgnore] public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        // ── Factory ───────────────────────────────────────────────────────────

        public static FloorMapDocument FromAllBlocks(
            int entityId, string playfield, string solarSystem, JObject blocks)
        {
            return new FloorMapDocument
            {
                EntityId    = entityId,
                Playfield   = playfield,
                SolarSystem = solarSystem,
                CapturedAt  = DateTime.UtcNow.ToString("o"),
                Blocks      = blocks ?? new JObject(),
            };
        }

        // Copies all data from source and re-renders at source.Y.
        public void UpdateFrom(FloorMapDocument source)
        {
            EntityId    = source.EntityId;
            Playfield   = source.Playfield;
            SolarSystem = source.SolarSystem;
            CapturedAt  = source.CapturedAt;
            Y           = source.Y;
            PlayerX     = source.PlayerX;
            PlayerZ     = source.PlayerZ;
            Blocks      = source.Blocks;
            Render();
        }

        // ── Persistence ───────────────────────────────────────────────────────

        public static FloorMapDocument? Load(string path)
        {
            try
            {
                var doc = JsonConvert.DeserializeObject<FloorMapDocument>(File.ReadAllText(path));
                doc?.Render();
                return doc;
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"FloorMapDocument.Load failed ({path}): {ex.Message}");
                return null;
            }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        // ── Render ────────────────────────────────────────────────────────────

        // Filters Blocks to Y (walls) and Y-1 (floors) then draws the map.
        public void Render()
        {
            var cols = Blocks?["Columns"] as JArray;
            var rows = Blocks?["Rows"]    as JArray;
            if (cols == null || rows == null || rows.Count == 0)
            {
                StatusText = "No data";
                return;
            }

            var colIndex = new Dictionary<string, int>();
            for (int i = 0; i < cols.Count; i++) colIndex[(string)cols[i]] = i;

            int ixX     = colIndex["X"];
            int ixY     = colIndex["Y"];
            int ixZ     = colIndex["Z"];
            int ixType  = colIndex["Type"];
            int ixShape = colIndex["Shape"];
            int ixRot   = colIndex["Rotation"];
            int minCols = Math.Max(Math.Max(ixX, ixY), Math.Max(Math.Max(ixZ, ixType), Math.Max(ixShape, ixRot))) + 1;

            var wallBlocks  = new List<BlockEntry>();
            var floorBlocks = new List<BlockEntry>();

            foreach (JToken row in rows)
            {
                var arr = row as JArray;
                if (arr == null || arr.Count < minCols) continue;
                int bx   = (int)(arr[ixX]    ?? 0);
                int by   = (int)(arr[ixY]    ?? 0);
                int bz   = (int)(arr[ixZ]    ?? 0);
                int type = (int)(arr[ixType] ?? 0);
                if (type == 0 || SkippedTypes.Contains(type)) continue;
                int shape = (int)(arr[ixShape] ?? 0);
                int rot   = (int)(arr[ixRot]   ?? 0);

                var entry = new BlockEntry { X = bx, Z = bz, Type = type, Shape = shape, Rotation = rot };
                if      (by == Y)     wallBlocks .Add(entry);
                else if (by == Y - 1) floorBlocks.Add(entry);
            }

            if (wallBlocks.Count == 0 && floorBlocks.Count == 0)
            {
                StatusText = $"No blocks at Y={Y}";
                return;
            }

            int minX = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var b in wallBlocks.Concat(floorBlocks))
            {
                if (b.X < minX) minX = b.X;
                if (b.X > maxX) maxX = b.X;
                if (b.Z < minZ) minZ = b.Z;
                if (b.Z > maxZ) maxZ = b.Z;
            }
            int width  = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            var wallDict  = wallBlocks .ToDictionary(b => (b.X, b.Z));
            var floorDict = floorBlocks.ToDictionary(b => (b.X, b.Z));

            // Block coords -> canvas coords: transpose (swap X and Z axes).
            //   canvas_x = gz * TileSize
            //   canvas_y = gx * TileSize
            // Canvas is Height wide x Width tall (Z range horizontal, X range vertical).

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                for (int gz = 0; gz < height; gz++)
                {
                    for (int gx = 0; gx < width; gx++)
                    {
                        int bx  = gx + minX;
                        int bz  = gz + minZ;
                        var key = (bx, bz);

                        bool hasWall  = wallDict .ContainsKey(key);
                        bool hasFloor = floorDict.ContainsKey(key);
                        if (!hasWall && !hasFloor) continue;

                        int cx = gz * TileSize;
                        int cy = gx * TileSize;
                        var cellRect = new Rect(cx, LabelTop + cy, TileSize, TileSize);
                        var block    = hasWall ? wallDict[key] : floorDict[key];

                        TypeCategory.TryGetValue(block.Type, out var category);

                        var brush = category == TileCategory.Door    ? WallBrush
                                  : category == TileCategory.Console ? FloorBrush
                                  : category == TileCategory.Walkway ? WalkwayBrush
                                  : hasWall && hasFloor              ? WallFloorBrush
                                  : hasWall                          ? WallBrush
                                                                     : FloorBrush;

                        var labelText = category == TileCategory.Door    ? "D"
                                      : category == TileCategory.Console ? "C"
                                                                         : block.Type.ToString();

                        dc.DrawGeometry(brush, null, ShapeGeometry.Get(block.Shape, block.Rotation, cellRect));
                        dc.DrawRectangle(null, GridPen, cellRect);

                        var lbl = MakeLabel(labelText);
                        dc.DrawText(lbl, new Point(
                            cellRect.X + (TileSize - lbl.Width)  / 2.0,
                            cellRect.Y + (TileSize - lbl.Height) / 2.0));
                    }
                }

                // Top labels: Z values (horizontal axis)
                for (int gz = 0; gz < height; gz++)
                {
                    var lbl = MakeLabel((gz + minZ).ToString());
                    dc.DrawText(lbl, new Point(gz * TileSize + (TileSize - lbl.Width) / 2.0,
                                               (LabelTop - lbl.Height) / 2.0));
                }
                // Right labels: X values (vertical axis)
                for (int gx = 0; gx < width; gx++)
                {
                    var lbl = MakeLabel((gx + minX).ToString());
                    dc.DrawText(lbl, new Point(height * TileSize + 4,
                                               LabelTop + gx * TileSize + (TileSize - lbl.Height) / 2.0));
                }

                int pgx = PlayerX - minX;
                int pgz = PlayerZ - minZ;
                if (pgx >= 0 && pgx < width && pgz >= 0 && pgz < height)
                    dc.DrawEllipse(PlayerBrush, null,
                        new Point(pgz * TileSize + TileSize / 2.0,
                                  LabelTop + pgx * TileSize + TileSize / 2.0), 4, 4);
            }

            int pixW = height * TileSize + LabelRight;
            int pixH = LabelTop + width  * TileSize;
            var rtb  = new RenderTargetBitmap(pixW, pixH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            MapImage   = rtb;
            StatusText = $"Entity {EntityId}  Y={Y}  {width}x{height}  {wallBlocks.Count + floorBlocks.Count} blocks";
        }

        private static FormattedText MakeLabel(string text) =>
            new FormattedText(text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFace, LabelFontSize, LabelBrush, 1.0);

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
