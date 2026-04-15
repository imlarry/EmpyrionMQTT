namespace EDNAClient.Skills.FloorMap
{
    public partial class FloorMapView : System.Windows.Controls.UserControl
    {
        public FloorMapView(FloorMapViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
