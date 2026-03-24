using System.Windows;
using EDNAClient.Core;

namespace EDNAClient.Skills.StatusPill
{
    public partial class HudWindow : Window
    {
        public HudWindow(HudViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public void SnapToGameWindow()
        {
            var pillWidth  = (double)Application.Current.Resources["PillWidth"];
            var pillHeight = (double)Application.Current.Resources["PillHeight"];
            var edgeInset  = (double)Application.Current.Resources["PillEdgeInset"];

            var gameRect = GameWindowLocator.GetClientRect()
                ?? new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

            Left = gameRect.Left + (gameRect.Width - pillWidth) / 2;
            Top  = gameRect.Top  + edgeInset;
        }
    }
}
