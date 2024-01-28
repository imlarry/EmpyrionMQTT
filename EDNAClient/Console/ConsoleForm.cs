using System;
using System.Windows.Forms;

namespace EDNAClient
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

    public class ConsoleForm : Form
    {
        public ConsoleForm(TabControl tabControl)
        {
            // Create the layout and consoles
            SplitContainer splitContainer = CreateSplitContainer();
            NonFocusableRichTextBox outputConsole = CreateOutputConsole();
            TextBox inputConsole = CreateInputConsole(outputConsole);
            splitContainer.Panel1.Controls.Add(outputConsole);
            splitContainer.Panel2.Controls.Add(inputConsole);

            // Set the height of the inputConsole to 30 pixels
            splitContainer.FixedPanel = FixedPanel.Panel2;
            splitContainer.SplitterDistance = splitContainer.Height - 30;

            // Create a new TabPage
            TabPage consoleTab = new("Console");

            // Add the SplitContainer to the TabPage
            consoleTab.Controls.Add(splitContainer);

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

        private SplitContainer CreateSplitContainer()
        {
            SplitContainer splitContainer = new()
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };
            return splitContainer;
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