using System.Windows;
using System.Windows.Input;
using Point = System.Windows.Point;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;

namespace EDNAClient.Skills.FloorMap
{
    public partial class FloorMapPanel : System.Windows.Controls.UserControl
    {
        private const double MinScale = 0.25;
        private const double MaxScale = 8.0;
        private const double ZoomStep = 1.2;

        private double _scale = 1.0;

        public FloorMapPanel(FloorMapDocument document)
        {
            InitializeComponent();
            DataContext = document;
            PreviewMouseWheel += OnPreviewMouseWheel;
        }

        // Cursor-centered zoom: the bitmap point under the cursor stays put.
        // PreviewMouseWheel runs before ScrollViewer's own wheel-scroll, and we
        // mark e.Handled so the scroller doesn't also pan.
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MapScroller == null || MapImage == null) return;

            // Cursor in scroll-viewer pixel coords (the visible viewport).
            Point cursorInScroll = e.GetPosition(MapScroller);

            // Cursor in image-local (pre-scale) coords -- the underlying bitmap pixel.
            Point cursorInImage = e.GetPosition(MapImage);

            double newScale = e.Delta > 0 ? _scale * ZoomStep : _scale / ZoomStep;
            if (newScale < MinScale) newScale = MinScale;
            if (newScale > MaxScale) newScale = MaxScale;
            if (newScale == _scale) { e.Handled = true; return; }

            _scale = newScale;
            MapScale.ScaleX = _scale;
            MapScale.ScaleY = _scale;
            MapImage.UpdateLayout();

            // After scaling, place cursorInImage at the same screen offset by
            // adjusting the scroll offsets: scrollOffset = imagePt * scale - cursorInScroll.
            double newH = cursorInImage.X * _scale - cursorInScroll.X;
            double newV = cursorInImage.Y * _scale - cursorInScroll.Y;
            MapScroller.ScrollToHorizontalOffset(newH);
            MapScroller.ScrollToVerticalOffset(newV);

            e.Handled = true;
        }
    }
}
