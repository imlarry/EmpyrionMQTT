using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EDNAClient.Core;

namespace EDNAClient.Skills.ThreatRadar
{
    public partial class ThreatWindow : Window
    {
        private readonly ThreatViewModel _viewModel;

        public ThreatWindow(ThreatViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelChanged;
            // Snap once WPF has completed initial layout — guards against
            // the Window measuring the Viewbox canvas (200×200) as its natural size.
            Loaded += (_, _) => SnapToGameWindow();
        }

        // ── Snap to game window ────────────────────────────────────────────

        public void SnapToGameWindow()
        {
            var gameRect = GameWindowLocator.GetClientRect()
                ?? new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

            double size = gameRect.Height / 3.0;
            Width  = size;
            Height = size;
            Left   = gameRect.Left + (gameRect.Width  - size) / 2;
            Top    = gameRect.Top  + (gameRect.Height - size) / 2;
        }

        // ── ViewModel change handler ───────────────────────────────────────

        private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateArc(NorthArc, _viewModel.NorthThreat);
            UpdateArc(EastArc,  _viewModel.EastThreat);
            UpdateArc(SouthArc, _viewModel.SouthThreat);
            UpdateArc(WestArc,  _viewModel.WestThreat);

            if (_viewModel.HasThreats && !IsVisible)
            {
                SnapToGameWindow();
                Show();
            }
            else if (!_viewModel.HasThreats && IsVisible)
            {
                Hide();
            }
        }

        // ── Arc animation ──────────────────────────────────────────────────

        private static void UpdateArc(Path arc, ThreatLevel level)
        {
            // Clear any running animation and snap back to base value
            arc.BeginAnimation(OpacityProperty, null);

            switch (level)
            {
                case ThreatLevel.None:
                    arc.Opacity = 0;
                    break;

                case ThreatLevel.Near:
                    arc.BeginAnimation(OpacityProperty, MakePulse(1.0));
                    break;

                case ThreatLevel.Close:
                    arc.BeginAnimation(OpacityProperty, MakePulse(0.33));
                    break;

                case ThreatLevel.Imminent:
                    arc.Opacity = 1.0;
                    break;
            }
        }

        private static DoubleAnimation MakePulse(double periodSeconds) =>
            new DoubleAnimation(0.15, 1.0, new Duration(TimeSpan.FromSeconds(periodSeconds)))
            {
                AutoReverse      = true,
                RepeatBehavior   = RepeatBehavior.Forever,
                EasingFunction   = new SineEase()
            };
    }
}
