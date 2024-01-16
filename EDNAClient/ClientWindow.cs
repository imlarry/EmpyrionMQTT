using System;
using System.Windows.Forms;

namespace EDNAClient
{

    public partial class ClientWindow : Form
    {
        public ClientWindow()
        {
            InitializeComponent();

            // add the bookmarks feature
            _ = new Bookmarks(treeView);
        }
    }
}
