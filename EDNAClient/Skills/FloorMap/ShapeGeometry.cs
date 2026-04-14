using System.Collections.Generic;
using System.Windows.Media;
using Point = System.Windows.Point;
using Rect  = System.Windows.Rect;

namespace EDNAClient.Skills.FloorMap
{
    // Top-down block shape geometry catalog.
    //
    // Empyrion exposes shape IDs 0-N via IBlock.Get(). The full set is undocumented;
    // IDs are added here as they are identified from live scan data.
    //
    // Each entry is a factory that returns a Geometry for the given cell Rect,
    // already transformed for the block's rotation. For unknown shape IDs the
    // fallback is a full rectangle (the safe conservative choice).
    //
    // Rotation semantics (approximate, empirical):
    //   Empyrion rotation values 0-23 encode 6 face-up orientations x 4 yaw steps.
    //   For a top-down view only the horizontal yaw matters. We use (rotation % 4) * 90
    //   as the yaw in degrees -- this is correct when the block top face is up (the
    //   common case) and a reasonable approximation otherwise.
    //
    // Shape upgrade path:
    //   When Path B (texture atlas) is implemented, replace the GetGeometry call in
    //   FloorMapViewModel.Render with DrawImage(atlas[blockType], rect). The shape
    //   geometry catalog can be retired or kept for overlay/wireframe rendering.
    internal static class ShapeGeometry
    {
        // Maps shape ID to a normalized polygon (vertices in 0..1 space, origin = top-left).
        // null = full rectangle (default fallback).
        private static readonly Dictionary<int, Point[]?> _shapes = new()
        {
            // Full cube -- entire cell
            [0] = null,   // null == use RectangleGeometry(rect)
            [1] = null,

            // Slope (right-angle ramp): top-down view is a right triangle.
            // Rotation 0: hypotenuse from top-left to bottom-right.
            // The triangle fills the bottom-left corner.
            [2] = new Point[] { new(0, 0), new(1, 1), new(0, 1) },

            // Thin wedge / corner piece: L-shaped or small triangle.
            // Placeholder -- update shape ID and vertices from live data.
            // [3] = new Point[] { ... },

            // Half slab (horizontal): from above looks like a full square (same XZ footprint).
            // Indistinguishable from a full cube in top-down projection -- leave as full rect.
            // [4] = null,
        };

        // Returns a Geometry for the given block shape, rotated by the block's rotation,
        // clipped to cellRect. Never returns null -- falls back to RectangleGeometry.
        public static Geometry Get(int shapeId, int rotation, Rect cellRect)
        {
            _shapes.TryGetValue(shapeId, out var vertices);

            if (vertices == null)
                return new RectangleGeometry(cellRect);

            // Degrees of clockwise rotation for the top-down view.
            double degrees = (rotation % 4) * 90.0;

            // Scale normalized vertices into cellRect space.
            var figure = new PathFigure { IsClosed = true, IsFilled = true };
            bool first = true;
            foreach (var v in vertices)
            {
                var pt = new Point(
                    cellRect.X + v.X * cellRect.Width,
                    cellRect.Y + v.Y * cellRect.Height);
                if (first) { figure.StartPoint = pt; first = false; }
                else figure.Segments.Add(new LineSegment(pt, isStroked: false));
            }

            var path = new PathGeometry();
            path.Figures.Add(figure);

            if (degrees != 0.0)
            {
                // Rotate around cell center.
                var center = new Point(
                    cellRect.X + cellRect.Width  / 2.0,
                    cellRect.Y + cellRect.Height / 2.0);
                path.Transform = new RotateTransform(degrees, center.X, center.Y);
            }

            return path;
        }
    }
}
