using System.Windows;
using EDNAClient.Core;

namespace EDNAClient.Skills.FloorMap
{
    public partial class FloorMapWindow : Window
    {
        private readonly FloorMapViewModel _viewModel;
        private FloorMapper? _mapper;

        public FloorMapWindow(FloorMapViewModel viewModel)
        {
            InitializeComponent();
            _viewModel  = viewModel;
            DataContext = viewModel;
        }

        internal void SetMapper(FloorMapper mapper) => _mapper = mapper;

        // ── Snap to a sensible default position near the game window ──────────

        public void SnapToGameWindow()
        {
            var gameRect = GameWindowLocator.GetClientRect();
            if (gameRect == null) return;

            // Position the map window to the right of the game window, or fall
            // back to the primary screen right edge.
            double left = gameRect.Value.Right + 8;
            double top  = gameRect.Value.Top;

            if (left + Width > SystemParameters.PrimaryScreenWidth)
                left = SystemParameters.PrimaryScreenWidth - Width - 8;

            Left = left;
            Top  = top;
        }

        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            if (_mapper == null) return;
            _viewModel.StatusText = "Scanning...";
            _viewModel.IsLoading  = true;
            _ = _mapper.RefreshAsync();
        }
    }
}
