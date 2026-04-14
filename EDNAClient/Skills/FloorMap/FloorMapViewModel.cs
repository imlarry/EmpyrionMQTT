using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace EDNAClient.Skills.FloorMap
{
    // Block data from one ScanFloor response entry.
    internal struct BlockInfo
    {
        public int Type;
        public int Shape;
        public int Rotation;
    }

    public class FloorMapViewModel : INotifyPropertyChanged
    {
        private const int TileSize   = 32;
        private const int LabelTop   = 20;  // top margin for column (X) headers
        private const int LabelRight = 52;  // right margin for row (Z) labels

        // Classification brushes -- frozen for thread safety.
        private static readonly Brush WallBrush      = Freeze(new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)));
        private static readonly Brush FloorBrush     = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)));
        private static readonly Brush WallFloorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x68, 0x68, 0x68)));
        private static readonly Brush WalkwayBrush   = Freeze(new SolidColorBrush(Color.FromRgb(0x50, 0x50, 0x50)));
        private static readonly Brush PlayerBrush    = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0xCC)));

        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        // ── Tile category overrides ───────────────────────────────────────────
        // Groups block type IDs into display categories that control tile color
        // and label text independently of the wall/floor classification.

        private enum TileCategory { None, Door, Console, Walkway }

        private static readonly Dictionary<int, TileCategory> TypeCategory =
            new Dictionary<int, TileCategory>
            {
                // Doors
                [1817] = TileCategory.Door,    [1886] = TileCategory.Door,
                [1816] = TileCategory.Door,    [2014] = TileCategory.Door,
                // Consoles
                [258]  = TileCategory.Console, [261]  = TileCategory.Console,
                [1243] = TileCategory.Console, [635]  = TileCategory.Console,
                [727]  = TileCategory.Console, [1469] = TileCategory.Console,
                [1087] = TileCategory.Console, [636]  = TileCategory.Console,
                // Walkways
                [884]  = TileCategory.Walkway, [972]  = TileCategory.Walkway,
            };

        private static readonly System.Windows.Media.Pen GridPen   = FreezePen(new System.Windows.Media.Pen(Brushes.White, 1.0));
        private static readonly Brush    LabelBrush = Brushes.White;
        private static readonly Typeface LabelFace  = new Typeface("Consolas");
        private const  double            LabelFontSize = 8.5;

        private static System.Windows.Media.Pen FreezePen(System.Windows.Media.Pen p) { p.Freeze(); return p; }

        // ── Bindable properties ───────────────────────────────────────────────

        private BitmapSource? _mapImage;
        private string _statusText = "Ready";
        private bool _isLoading;

        public BitmapSource? MapImage
        {
            get => _mapImage;
            private set { _mapImage = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // ── Render ────────────────────────────────────────────────────────────

        // Called from the UI thread after a successful pair of ScanFloor responses.
        //   wallBlocks:  blocks at the player's Y level (walls / eye-level obstacles)
        //   floorBlocks: blocks at Y-1 (floor, underfoot)
        //   minX/minZ:   struct-space origin of the combined bounding box
        //   width/height:number of cells in X and Z
        //   playerX/Z:   player struct-space position (used for the cyan dot overlay)
        //   status:      text to display in the status bar
        internal void Render(
            IReadOnlyDictionary<(int X, int Z), BlockInfo> wallBlocks,
            IReadOnlyDictionary<(int X, int Z), BlockInfo> floorBlocks,
            int minX, int minZ, int width, int height,
            int playerX, int playerZ,
            string status)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                for (int gz = 0; gz < height; gz++)
                {
                    for (int gx = 0; gx < width; gx++)
                    {
                        int bx = gx + minX;
                        int bz = gz + minZ;
                        var key = (bx, bz);

                        bool hasWall  = wallBlocks.ContainsKey(key);
                        bool hasFloor = floorBlocks.ContainsKey(key);
                        if (!hasWall && !hasFloor) continue;

                        var cellRect = new Rect(gx * TileSize, LabelTop + gz * TileSize, TileSize, TileSize);

                        // Use the wall-level block's shape when present, else the floor block.
                        var block = hasWall ? wallBlocks[key] : floorBlocks[key];

                        TypeCategory.TryGetValue(block.Type, out var category);

                        // Category overrides brush and label; fallback to wall/floor classification.
                        var brush = category == TileCategory.Door    ? WallBrush
                                  : category == TileCategory.Console ? FloorBrush
                                  : category == TileCategory.Walkway ? WalkwayBrush
                                  : hasWall && hasFloor              ? WallFloorBrush
                                  : hasWall                          ? WallBrush
                                                                     : FloorBrush;
                        var labelText = category == TileCategory.Door    ? "D"
                                      : category == TileCategory.Console ? "C"
                                                                         : block.Type.ToString();

                        var geo = ShapeGeometry.Get(block.Shape, block.Rotation, cellRect);
                        dc.DrawGeometry(brush, null, geo);

                        // White grid border.
                        dc.DrawRectangle(null, GridPen, cellRect);

                        // Label centered on tile.
                        var typeLabel = MakeLabel(labelText);
                        dc.DrawText(typeLabel, new Point(
                            cellRect.X + (TileSize - typeLabel.Width)  / 2.0,
                            cellRect.Y + (TileSize - typeLabel.Height) / 2.0));
                    }
                }

                // Column headers: block X coordinate above each column.
                for (int gx = 0; gx < width; gx++)
                {
                    var lbl = MakeLabel((gx + minX).ToString());
                    dc.DrawText(lbl, new Point(
                        gx * TileSize + (TileSize - lbl.Width) / 2.0,
                        (LabelTop - lbl.Height) / 2.0));
                }

                // Row labels: block Z coordinate to the right of each row.
                for (int gz = 0; gz < height; gz++)
                {
                    var lbl = MakeLabel((gz + minZ).ToString());
                    dc.DrawText(lbl, new Point(
                        width * TileSize + 4,
                        LabelTop + gz * TileSize + (TileSize - lbl.Height) / 2.0));
                }

                // Player dot -- draw regardless of whether the player cell has a block.
                int playerGX = playerX - minX;
                int playerGZ = playerZ - minZ;
                if (playerGX >= 0 && playerGX < width && playerGZ >= 0 && playerGZ < height)
                {
                    double cx = playerGX * TileSize + TileSize / 2.0;
                    double cz = LabelTop + playerGZ * TileSize + TileSize / 2.0;
                    dc.DrawEllipse(PlayerBrush, null, new Point(cx, cz), 4, 4);
                }
            }

            int pixW = width  * TileSize + LabelRight;
            int pixH = LabelTop + height * TileSize;
            var rtb = new RenderTargetBitmap(pixW, pixH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();

            MapImage   = rtb;
            StatusText = status;
        }

        private static FormattedText MakeLabel(string text)
        {
            return new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                LabelFace,
                LabelFontSize,
                LabelBrush,
                1.0);
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
