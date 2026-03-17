using System.Windows;
using System.Windows.Input;
using EDNAClient.ViewModels;

namespace EDNAClient
{
    public partial class HudWindow : Window
    {
        public HudWindow(HudViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void MenuBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
