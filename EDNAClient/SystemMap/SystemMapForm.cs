using System.Windows.Forms;
using CommandLine.Text;
using System.Windows.Forms.VisualStyles;
using ESB.Database;
using ESB.TopicHandlers;
using Newtonsoft.Json.Linq;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace EDNAClient
{
    public class Playfield
    {
        public int Ssid { get; set; }
        public int Pfid { get; set; }
        public string Name { get; set; }
        public int PlanetSize { get; set; }
        public int IconColor { get; set; }
        public int SectorX { get; set; }
        public int SectorY { get; set; }
        public int SectorZ { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
    }
    public class SystemMapForm : Form
    {
        private void PlotPoints(IEnumerable<Playfield> playfields)
        {
            GL.Begin(PrimitiveType.Points);

            foreach (var playfield in playfields)
            {
                float x = playfield.SectorX / 100000f;
                float y = playfield.SectorY / 100000f;
                float z = playfield.SectorZ / 100000f;

                GL.Vertex3(x, y, z);
            }

            GL.End();
        }
        public void PlotPointsFromJson(JObject json)
        {
            var playfields = json["Map"].ToObject<List<Playfield>>();
            PlotPoints(playfields);
        }
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

                // Fetch the data and plot the points
                DbAccess _dbAccess = new("Data Source=C:\\SteamRoot\\steamapps\\common\\Empyrion - Galactic Survival\\Saves\\Games\\Wanderlust\\global.db;Version=3;", true);
                JObject json = new();
                _dbAccess.JsonDataset(json, "Map", "select ssid,pfid,name,planetsize,iconcolor,sectorx,sectory,sectorz,posx,posy,posz from Playfields where pftype <> 2 and ssid <= 2", null);
                this.PlotPointsFromJson(json);
                var jsonString = json.ToString();
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
