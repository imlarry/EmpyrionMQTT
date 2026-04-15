namespace EDNAClient.Workspace
{
    public partial class NavigationView : System.Windows.Controls.UserControl
    {
        public NavigationView(NavigationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
