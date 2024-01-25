using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace EDNA
{
    public class SystemMapForm : Form
    {
        public SystemMapForm(TabControl tabControl)
        {
            // Create the layout
            TableLayoutPanel tableLayoutPanel = CreateTableLayoutPanel();

            // Create the GLControl for the system map
            GLControl systemMapControl = CreateSystemMapControl();
            tableLayoutPanel.Controls.Add(systemMapControl, 0, 0);

            // Set the GLControl to fill its parent control
            systemMapControl.Dock = DockStyle.Fill;

            // Create a new TabPage
            TabPage systemMapTab = new("System Map");

            // Add the TableLayoutPanel to the TabPage
            systemMapTab.Controls.Add(tableLayoutPanel);

            // Add the TabPage to the TabControl
            tabControl.TabPages.Add(systemMapTab);
        }

        private TableLayoutPanel CreateTableLayoutPanel()
        {
            TableLayoutPanel tableLayoutPanel = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 1
            };
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // systemMapControl row
            return tableLayoutPanel;
        }

        private GLControl CreateSystemMapControl()
        {
            GLControl systemMapControl = new()
            {
                Dock = DockStyle.Fill,
                VSync = true
            };
            systemMapControl.Load += (s, e) =>
            {
                GL.ClearColor(Color.Black);
            };
            systemMapControl.Paint += (s, e) =>
            {
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                // Add your 2D projection rendering code here
                systemMapControl.SwapBuffers();
            };
            return systemMapControl;
        }
    }
}