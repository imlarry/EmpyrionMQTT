namespace EDNAClient.Skills.FloorMap
{
    public partial class FloorMapPanel : System.Windows.Controls.UserControl
    {
        public FloorMapPanel(FloorMapDocument document)
        {
            InitializeComponent();
            DataContext = document;
        }
    }
}
