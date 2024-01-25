
using System.Windows.Forms;

namespace EDNA
{
    public class NonFocusableRichTextBox : RichTextBox
    {
        private const int WM_SETFOCUS = 0x0007;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETFOCUS)
                return; // Ignore the message

            base.WndProc(ref m);
        }
    }

    public partial class ConsoleForm : Form
    {
        public ConsoleForm(TabControl tabControl)
        {
            InitializeComponent();

            // Create the layout and consoles
            TableLayoutPanel tableLayoutPanel = CreateTableLayoutPanel();
            NonFocusableRichTextBox outputConsole = CreateOutputConsole();
            TextBox inputConsole = CreateInputConsole(outputConsole);
            tableLayoutPanel.Controls.Add(outputConsole, 0, 0);
            tableLayoutPanel.Controls.Add(inputConsole, 0, 1);

            // Set the TextBox to fill its parent control
            inputConsole.Dock = DockStyle.Fill;

            // Create a new TabPage
            TabPage consoleTab = new("Console");

            // Add the TableLayoutPanel to the TabPage
            consoleTab.Controls.Add(tableLayoutPanel);

            // Add the TabPage to the TabControl
            tabControl.TabPages.Add(consoleTab);

            // Oneshot focus to the inputConsole TextBox when the TabPage is added
            void IdleHandler(object s, EventArgs e)
            {
                inputConsole.Focus();
                inputConsole.SelectionStart = inputConsole.Text.Length;
                Application.Idle -= IdleHandler;
            }
            Application.Idle += IdleHandler;
        }

        private TableLayoutPanel CreateTableLayoutPanel()
        {
            TableLayoutPanel tableLayoutPanel = new()
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // consoleOutput row
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // consoleInput row
            return tableLayoutPanel;
        }

        private NonFocusableRichTextBox CreateOutputConsole()
        {
            NonFocusableRichTextBox outputConsole = new()
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                ShortcutsEnabled = false, // Disable context menu
                TabStop = false
            };
            return outputConsole;
        }

        private TextBox CreateInputConsole(NonFocusableRichTextBox outputConsole)
        {
            TextBox inputConsole = new()
            {
                Dock = DockStyle.Fill,
                Text = "EDNA> "
            };
            inputConsole.SelectionStart = inputConsole.Text.Length;
            inputConsole.KeyPress += (s, e) =>
            {
                if (inputConsole.SelectionStart < "EDNA> ".Length)
                {
                    e.Handled = true;
                    if (!char.IsControl(e.KeyChar)) inputConsole.Text += e.KeyChar;
                }
            };
            inputConsole.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // Prevent the beep
                    string input = inputConsole.Text.Substring("EDNA> ".Length);
                    outputConsole.AppendText(Environment.NewLine + "EDNA> " + input);
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        CommandLineParser.Evaluate(input, outputConsole);
                    }
                    outputConsole.ScrollToCaret();
                    inputConsole.Text = "EDNA> ";
                    inputConsole.SelectionStart = inputConsole.Text.Length;
                }
            };
            return inputConsole;
        }

    }
}
