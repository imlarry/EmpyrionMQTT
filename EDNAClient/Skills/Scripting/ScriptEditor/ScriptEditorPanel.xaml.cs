using System.IO;
using System.Reflection;
using System.Windows.Input;
using UserControl   = System.Windows.Controls.UserControl;
using KeyEventArgs  = System.Windows.Input.KeyEventArgs;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace EDNAClient.Skills.Scripting.ScriptEditor
{
    public partial class ScriptEditorPanel : UserControl
    {
        private readonly string       _filePath;
        private readonly Action<bool> _onDirtyChanged;
        private bool                  _isDirty;
        private bool                  _loading;

        public ScriptEditorPanel(string filePath, Action<bool> onDirtyChanged)
        {
            _filePath       = filePath;
            _onDirtyChanged = onDirtyChanged;

            InitializeComponent();
            LoadSyntaxHighlighting();
            LoadFile();

            Editor.TextChanged      += (_, _) => { if (!_loading) SetDirty(true); };
            Editor.PreviewKeyDown   += OnKeyDown;
        }

        public bool IsDirty => _isDirty;

        public void Save()
        {
            File.WriteAllText(_filePath, Editor.Text);
            SetDirty(false);
        }

        private void SetDirty(bool dirty)
        {
            if (_isDirty == dirty) return;
            _isDirty = dirty;
            _onDirtyChanged(dirty);
        }

        private void LoadFile()
        {
            _loading    = true;
            Editor.Text = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "";
            _loading    = false;
            SetDirty(false);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Save();
                e.Handled = true;
            }
        }

        private void LoadSyntaxHighlighting()
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("EDNAClient.Skills.Scripting.ScriptEditor.lua.xshd");
                if (stream == null) return;
                Editor.SyntaxHighlighting = HighlightingLoader.Load(
                    new XmlTextReader(stream),
                    HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                EDNAClient.Core.EdnaLogger.Warn($"Failed to load Lua syntax highlighting: {ex.Message}");
            }
        }
    }
}
