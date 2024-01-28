using System.Windows.Forms;
using System.Drawing;
using EDNA;
using ESB.Database;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace EDNAClient
{
    public class ClientWindow : Form
    {

        private readonly MenuStrip menuStrip; 
        private readonly StatusStrip statusStrip;
        private readonly TableLayoutPanel tableLayoutPanel;
        private readonly SplitContainer splitContainer;
        private readonly ToolStripMenuItem fileToolStripMenuItem;
        private readonly ToolStripMenuItem editToolStripMenuItem;
        private readonly ToolStripMenuItem viewToolStripMenuItem;
        private readonly ToolStripMenuItem helpToolStripMenuItem;
        private readonly ToolStripContainer toolStripContainer;
        private readonly ToolStrip toolStrip;
        private readonly ToolStripButton toolStripButton;
        private readonly TreeView treeView;
        private readonly TabControl tabControl;

        public ClientWindow()
        {
            // declare components
            menuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            statusStrip = new StatusStrip();
            tableLayoutPanel = new TableLayoutPanel();
            splitContainer = new SplitContainer();
            toolStripContainer = new ToolStripContainer();
            toolStrip = new ToolStrip();
            toolStripButton = new ToolStripButton();
            treeView = new TreeView();
            tabControl = new TabControl();

            // configure menu strip
            menuStrip.Name = "menuStrip";
            menuStrip.Dock = DockStyle.Fill;
            menuStrip.Items.AddRange(new ToolStripItem[] {
                fileToolStripMenuItem,
                editToolStripMenuItem,
                viewToolStripMenuItem,
                helpToolStripMenuItem
            });

            // configure menu strip items
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Text = "File";

            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Text = "Edit";

            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Text = "View";

            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Text = "Help";

            // configure tool strip container & items
            toolStripContainer.Name = "toolStripContainer";
            toolStripContainer.Dock = DockStyle.Fill;
            toolStripContainer.AutoSize = false;
            toolStripContainer.Height = 0; // toolStrip.Height;
            toolStripButton.Text = "My Button";
            toolStrip.Items.Add(toolStripButton);
            toolStripContainer.TopToolStripPanel.Controls.Add(toolStrip);

            // configure split container for left/right panels
            splitContainer.Dock = DockStyle.Fill;

            // configure status strip
            statusStrip.Name = "statusStrip";
            statusStrip.Dock = DockStyle.Bottom;

            // configure table layout
            tableLayoutPanel.Dock = DockStyle.Fill;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.RowCount = 3;
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // For MenuStrip
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // For ToolStripContainer
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // For SplitContainer


            // add the tree view to panel 1 of the split container
            treeView.Name = "treeView";
            treeView.Dock = DockStyle.Fill;
            treeView.LabelEdit = true;
            treeView.TabIndex = 0;

            // add the tab control to panel 2 of the split container
            tabControl.Name = "tabControl";
            tabControl.Dock = DockStyle.Fill;
            tabControl.SelectedIndex = 0;
            tabControl.SizeMode = TabSizeMode.FillToRight;

            // configure main form
            Name = "ClientWindow";
            Text = "EDNA";
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            MainMenuStrip = menuStrip;

            // add controls to form
            tableLayoutPanel.Controls.Add(menuStrip, 0, 0);
            tableLayoutPanel.Controls.Add(toolStripContainer, 0, 1);
            tableLayoutPanel.Controls.Add(splitContainer, 0, 2);
            splitContainer.Panel1.Controls.Add(treeView);
            splitContainer.Panel2.Controls.Add(tabControl);
            Controls.Add(tableLayoutPanel);
            Controls.Add(statusStrip);

            // add startup child forms
            _ = new Bookmarks(treeView);
            _ = new ConsoleForm(tabControl);
            _ = new SystemMapForm(tabControl);

        }
    }
}
