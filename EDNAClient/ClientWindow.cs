using EDNA;

namespace EDNAClient
{

    public partial class ClientWindow : Form
    {
        public ClientWindow()
        {
            InitializeComponent();

            // add the bookmarks feature
            _ = new Bookmarks(treeView);

            // Create an instance of the ConsoleForm
            _ = new ConsoleForm(tabControl1);

            // Create an instance of the SystemMapForm
            _ = new SystemMapForm(tabControl1);
        }
    }
}
