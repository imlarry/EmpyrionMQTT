namespace EDNAClient
{
    public class Bookmarks
    {
        private readonly TreeView _treeView;

        public Bookmarks(TreeView treeView)
        {
            _treeView = treeView;

            // Check if an instance already exists in the Context tree view
            if (FindNode("Bookmarks") != null)
            {
                throw new InvalidOperationException("An instance of the Bookmarks class already exists.");
            }

            // add a root level "feature"
            var bookmarksNode = _treeView.Nodes.Add("Bookmarks");

            // add predefined nodes for grouping based on bookmark properties
            var allNode = bookmarksNode.Nodes.Add("All");
            var systemNode = bookmarksNode.Nodes.Add("System");
            var localNode = bookmarksNode.Nodes.Add("Local");
            var nonPlayerNode = bookmarksNode.Nodes.Add("Non-player");

            // add separator node
            var line = new string('\u2500', 15);
            var separatorNode = bookmarksNode.Nodes.Insert(nonPlayerNode.Index + 1, line);

            // add a button node for creating new groups
            var createGroupNode = bookmarksNode.Nodes.Add("New group");
            createGroupNode.ForeColor = Color.Blue;
            createGroupNode.NodeFont = new System.Drawing.Font(_treeView.Font, System.Drawing.FontStyle.Italic);

            // add handler for clicking on leaf items in the treeview
            _treeView.AfterSelect += TreeView_AfterSelect;
            _treeView.AfterLabelEdit += TreeView_AfterLabelEdit;
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Text == "New group")
            {
                var newNode = e.Node.Parent.Nodes.Insert(e.Node.Index, "<enter group name>");

                _treeView.SelectedNode = newNode;
                _treeView.LabelEdit = true;
                if (!newNode.IsEditing)
                {
                    _treeView.BeginInvoke(new MethodInvoker(delegate { newNode.BeginEdit(); }));
                }
            }
        }

        // 
        /*
            if (mySelectedNode != null && mySelectedNode.Parent == null)
            {
                MessageBox.Show("Editing of root nodes is not allowed.", "Invalid selection");
            }
            else
            {
                MessageBox.Show("No tree node selected.", "Invalid selection");
            }
        }
        */

        private void TreeView_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Label != null)
            {
                if (e.Label.Length > 0)
                {
                    if (e.Label.IndexOfAny(new char[] { '@', '.', ',', '!' }) == -1)
                    {
                        // Stop editing without canceling the label change.
                        e.Node.EndEdit(false);
                    }
                    else
                    {
                        // Cancel the label edit action, inform the user, and place the node in edit mode again.
                        e.CancelEdit = true;
                        MessageBox.Show("Invalid tree node label.\n" +
                            "The invalid characters are: '@','.', ',', '!'",
                            "Node Label Edit");
                        e.Node.BeginEdit(); // does it restart edit?
                    }
                }
                else
                {
                    // Cancel the label edit action, inform the user, and place the node in edit mode again
                    e.CancelEdit = true;
                    MessageBox.Show("Invalid tree node label.\nThe label cannot be blank",
                        "Node Label Edit");
                    //e.Node.BeginEdit();
                    _treeView.BeginInvoke(new MethodInvoker(delegate { e.Node.BeginEdit(); }));
                }
            }
        }

        private TreeNode FindNode(string text)
        {
            foreach (TreeNode node in _treeView.Nodes)
            {
                if (node.Text == text)
                {
                    return node;
                }
            }
#pragma warning disable CS8603
            return null;
#pragma warning restore CS8603
        }
    }
}
