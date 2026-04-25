using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EDNAClient.Core;
using Newtonsoft.Json;
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

    // One captured floor level: raw scan data + metadata + rendered bitmap.
    // Serialised to / deserialised from JSON; BitmapSource is rebuilt on load.
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
        public string Playfield   { get; set; } = "";
        public string SolarSystem { get; set; } = "";
        public string CapturedAt  { get; set; } = "";
        public int    MinX        { get; set; }
        public int    MinZ        { get; set; }
        public int    Width       { get; set; }
        public int    Height      { get; set; }
        public int    PlayerX     { get; set; }
        public int    PlayerZ     { get; set; }

        public List<BlockEntry> WallBlocks  { get; set; } = new List<BlockEntry>();
        public List<BlockEntry> FloorBlocks { get; set; } = new List<BlockEntry>();

        // ── Runtime-only properties ───────────────────────────────────────────

        [JsonIgnore] public string DocumentId => $"{EntityId}_{Y}";
        [JsonIgnore] public string ShortTitle => $"Y={Y}";

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

        public static FloorMapDocument FromScan(
            int entityId, int y,
            string playfield, string solarSystem,
            int minX, int minZ, int width, int height,
            int playerX, int playerZ,
            IEnumerable<BlockEntry> wallBlocks,
            IEnumerable<BlockEntry> floorBlocks)
        {
            var doc = new FloorMapDocument
            {
                EntityId    = entityId,
                Y           = y,
                Playfield   = playfield,
                SolarSystem = solarSystem,
                CapturedAt  = DateTime.UtcNow.ToString("o"),
                MinX        = minX,  MinZ   = minZ,
                Width       = width, Height = height,
                PlayerX     = playerX, PlayerZ = playerZ,
                WallBlocks  = wallBlocks.ToList(),
                FloorBlocks = floorBlocks.ToList(),
            };
            return doc;
        }

        // Copies all scan data from source and re-renders.
        // Used when Ctrl+Shift+R refreshes an already-open document tab.
        public void UpdateFrom(FloorMapDocument source)
        {
            EntityId    = source.EntityId;    Y           = source.Y;
            Playfield   = source.Playfield;   SolarSystem = source.SolarSystem;
            CapturedAt  = source.CapturedAt;
            MinX        = source.MinX;        MinZ    = source.MinZ;
            Width       = source.Width;       Height  = source.Height;
            PlayerX     = source.PlayerX;     PlayerZ = source.PlayerZ;
            WallBlocks  = source.WallBlocks;
            FloorBlocks = source.FloorBlocks;
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
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        // ── Render ────────────────────────────────────────────────────────────

        // Rebuilds MapImage and StatusText from the stored block lists.
        public void Render()
        {
            if (Width <= 0 || Height <= 0) { StatusText = "No data"; return; }

            var wallDict  = WallBlocks .ToDictionary(b => (b.X, b.Z));
            var floorDict = FloorBlocks.ToDictionary(b => (b.X, b.Z));

            // Block coords -> canvas coords: transpose (swap X and Z axes).
            //   canvas_x = gz * TileSize
            //   canvas_y = gx * TileSize
            // Canvas is Height wide x Width tall (Z range horizontal, X range vertical).

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                for (int gz = 0; gz < Height; gz++)
                {
                    for (int gx = 0; gx < Width; gx++)
                    {
                        int bx  = gx + MinX;
                        int bz  = gz + MinZ;
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
                for (int gz = 0; gz < Height; gz++)
                {
                    var lbl = MakeLabel((gz + MinZ).ToString());
                    dc.DrawText(lbl, new Point(gz * TileSize + (TileSize - lbl.Width) / 2.0,
                                               (LabelTop - lbl.Height) / 2.0));
                }
                // Right labels: X values (vertical axis)
                for (int gx = 0; gx < Width; gx++)
                {
                    var lbl = MakeLabel((gx + MinX).ToString());
                    dc.DrawText(lbl, new Point(Height * TileSize + 4,
                                               LabelTop + gx * TileSize + (TileSize - lbl.Height) / 2.0));
                }

                int pgx = PlayerX - MinX;
                int pgz = PlayerZ - MinZ;
                if (pgx >= 0 && pgx < Width && pgz >= 0 && pgz < Height)
                    dc.DrawEllipse(PlayerBrush, null,
                        new Point(pgz * TileSize + TileSize / 2.0,
                                  LabelTop + pgx * TileSize + TileSize / 2.0), 4, 4);
            }

            int pixW = Height * TileSize + LabelRight;
            int pixH = LabelTop + Width  * TileSize;
            var rtb  = new RenderTargetBitmap(pixW, pixH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            MapImage   = rtb;
            StatusText = $"Entity {EntityId}  Y={Y}  {Width}x{Height}  {WallBlocks.Count + FloorBlocks.Count} blocks";
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
