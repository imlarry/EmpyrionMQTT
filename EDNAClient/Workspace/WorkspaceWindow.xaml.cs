using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using AvalonDock.Themes;
using EDNAClient.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace EDNAClient.Workspace
{
    public partial class WorkspaceWindow : Window
    {
        private readonly EdnaSettings        _settings;
        private readonly NavigationViewModel _navViewModel;
        private readonly List<IDockableSkill> _registeredSkills = new();

        public NavigationViewModel NavViewModel => _navViewModel;

        public WorkspaceWindow(EdnaSettings settings)
        {
            _settings     = settings;
            _navViewModel = new NavigationViewModel();

            // Apply saved bounds before InitializeComponent so the window
            // appears at the right position on first paint.
            if (_settings.WorkspaceBounds is { } bounds)
            {
                Left   = bounds.Left;
                Top    = bounds.Top;
                Width  = bounds.Width;
                Height = bounds.Height;
            }

            InitializeComponent();

            // Apply VS2013 dark theme. The class lives in AvalonDock.Themes.VS2013.dll
            // under the AvalonDock.Themes namespace. If this throws, the theme package
            // version may have moved the type -- remove this line to fall back to default.
            ApplyTheme();

            NavPane.Content = new NavigationView(_navViewModel);

            // Defer layout restore until the visual tree is fully built.
            Loaded += OnLoaded;
        }

        // ── Theme ────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            try
            {
                // Discover the VS2013 dark theme type at runtime to avoid XAML namespace issues.
                var asm = System.Reflection.Assembly.Load("AvalonDock.Themes.VS2013");
                var type = asm.GetType("AvalonDock.Themes.VS2013DarkTheme")
                        ?? asm.GetType("AvalonDock.Themes.Vs2013DarkTheme");
                if (type != null && Activator.CreateInstance(type) is AvalonDock.Themes.Theme theme)
                    DockManager.Theme = theme;
            }
            catch { /* Fall back to default AvalonDock theme silently */ }
        }

        // ── Skill tab management ──────────────────────────────────────────────

        /// <summary>
        /// Register dockable skills before the window is shown. Feeds the
        /// layout serialization callback so panes can be re-attached after
        /// restoring a saved layout.
        /// </summary>
        public void RegisterSkills(IEnumerable<IDockableSkill> skills)
        {
            foreach (var skill in skills)
                _registeredSkills.Add(skill);
        }

        /// <summary>
        /// Adds a tab for the skill, or activates the existing one.
        /// Call from IDockableSkill.StartAsync().
        /// </summary>
        public void AddSkillTab(IDockableSkill skill)
        {
            // Guard: don't add duplicates if StartAsync is called again.
            foreach (var child in MapPane.Children)
            {
                if (child is LayoutDocument existing && existing.ContentId == skill.Id)
                {
                    existing.IsActive = true;
                    return;
                }
            }

            var doc = new LayoutDocument
            {
                Title     = skill.Title,
                ContentId = skill.Id,
                Content   = skill.CreatePanel(),
                CanClose  = false,
            };
            MapPane.Children.Add(doc);
            doc.IsActive = true;
        }

        /// <summary>
        /// Removes the tab for the given skill. Call from IDockableSkill.Stop().
        /// </summary>
        public void RemoveSkillTab(string skillId)
        {
            for (int i = MapPane.Children.Count - 1; i >= 0; i--)
            {
                if (MapPane.Children[i] is LayoutDocument ld && ld.ContentId == skillId)
                {
                    MapPane.Children.RemoveAt(i);
                    break;
                }
            }
        }

        // ── Game window snapping ──────────────────────────────────────────────

        /// <summary>
        /// Position the workspace to the right of the game client area.
        /// Falls back to the right edge of the primary screen if no space.
        /// </summary>
        public void SnapToGameWindow()
        {
            var gameRect = GameWindowLocator.GetClientRect();
            if (gameRect == null) return;

            double left = gameRect.Value.Right + 8;
            double top  = gameRect.Value.Top;

            if (left + Width > SystemParameters.PrimaryScreenWidth)
                left = SystemParameters.PrimaryScreenWidth - Width - 8;

            if (top + Height > SystemParameters.PrimaryScreenHeight)
                top  = SystemParameters.PrimaryScreenHeight - Height - 8;
            if (top < 0) top = 0;

            Left = left;
            Top  = top;
        }

        // ── Layout persistence ────────────────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.WorkspaceLayout)) return;
            try
            {
                var serializer = new XmlLayoutSerializer(DockManager);
                serializer.LayoutSerializationCallback += OnLayoutSerializationCallback;
                using var reader = new StringReader(_settings.WorkspaceLayout);
                serializer.Deserialize(reader);
            }
            catch
            {
                // Corrupt layout XML -- silently fall back to the default XAML layout.
            }
        }

        private void OnLayoutSerializationCallback(
            object? sender, LayoutSerializationCallbackEventArgs e)
        {
            // AvalonDock cannot reconstruct live WPF content from XML alone.
            // Match each deserialized LayoutDocument by ContentId and re-attach the panel.
            if (e.Model is LayoutDocument ld)
            {
                foreach (var skill in _registeredSkills)
                {
                    if (skill.Id == ld.ContentId)
                    {
                        ld.Content = skill.CreatePanel();
                        e.Cancel   = false;
                        return;
                    }
                }
                // Unknown ContentId (skill removed) -- drop the pane gracefully.
                e.Cancel = true;
            }
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            // Intercept user close: save state and hide.
            // The workspace persists across game Stop/Start cycles.
            // ForceClose() is called from App.OnExit for true shutdown.
            e.Cancel = true;
            SaveAndHide();
        }

        internal void SaveAndHide()
        {
            try
            {
                var serializer = new XmlLayoutSerializer(DockManager);
                using var sw = new StringWriter();
                serializer.Serialize(sw);
                _settings.WorkspaceLayout = sw.ToString();
            }
            catch
            {
                _settings.WorkspaceLayout = null;
            }

            _settings.WorkspaceBounds = new Rect(Left, Top, Width, Height);
            _settings.Save();
            Hide();
        }

        /// <summary>
        /// Called from App.OnExit to allow the window to actually close
        /// (bypasses the save-and-hide intercept).
        /// </summary>
        internal void ForceClose()
        {
            Closing -= OnWindowClosing;
            Close();
        }
    }
}
