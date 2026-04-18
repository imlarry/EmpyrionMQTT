using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using EDNAClient.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace EDNAClient.Workspace
{
    public partial class WorkspaceWindow : Window
    {
        private readonly EdnaSettings         _settings;
        private readonly NavigationViewModel  _navViewModel;
        private readonly List<IDockableSkill> _registeredSkills = new();

        // Live references into the active layout tree.
        // Initialized from the XAML x:Name bindings; re-pointed after each deserialization
        // because XmlLayoutSerializer replaces the entire layout tree.
        private LayoutAnchorable   _navPane;
        private LayoutDocumentPane _mapPane;

        private readonly CancelEventHandler _closingHandler;

        public NavigationViewModel NavViewModel => _navViewModel;

        public WorkspaceWindow(EdnaSettings settings)
        {
            _settings     = settings;
            _navViewModel = new NavigationViewModel();

            InitializeComponent();
            ApplyTheme();

            _navPane = NavPane;
            _mapPane = MapPane;

            if (_settings.WorkspaceBounds is { } bounds)
            {
                Left   = bounds.Left;
                Top    = bounds.Top;
                Width  = bounds.Width;
                Height = bounds.Height;
            }

            var navView = new NavigationView(_navViewModel);
            _navPane.Content = navView;

            RestoreLayout(navView);

            _closingHandler = (s, e) => { e.Cancel = true; SaveAndHide(); };
            Closing += _closingHandler;
        }

        // ── Theme ────────────────────────────────────────────────────────────

        private void ApplyTheme()
        {
            try
            {
                var asm  = System.Reflection.Assembly.Load("AvalonDock.Themes.VS2013");
                var type = asm.GetType("AvalonDock.Themes.VS2013DarkTheme")
                        ?? asm.GetType("AvalonDock.Themes.Vs2013DarkTheme");
                if (type != null && Activator.CreateInstance(type) is AvalonDock.Themes.Theme theme)
                    DockManager.Theme = theme;
            }
            catch (Exception ex) { EdnaLogger.Warn($"ApplyTheme failed: {ex.Message}"); }
        }

        // ── Skill tab management ──────────────────────────────────────────────

        public void RegisterSkills(IEnumerable<IDockableSkill> skills)
        {
            foreach (var skill in skills)
                _registeredSkills.Add(skill);
        }

        public void AddSkillTab(IDockableSkill skill)
        {
            foreach (var child in _mapPane.Children)
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
            _mapPane.Children.Add(doc);
            doc.IsActive = true;
        }

        public void RemoveSkillTab(string skillId)
        {
            for (int i = _mapPane.Children.Count - 1; i >= 0; i--)
            {
                if (_mapPane.Children[i] is LayoutDocument ld && ld.ContentId == skillId)
                {
                    _mapPane.Children.RemoveAt(i);
                    break;
                }
            }
        }

        // ── Game window snapping ──────────────────────────────────────────────

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

        // ── Multi-document support ────────────────────────────────────────────

        public LayoutDocument OpenDocument(string title, string contentId, UIElement content)
        {
            foreach (var child in _mapPane.Children)
            {
                if (child is LayoutDocument ld && ld.ContentId == contentId)
                {
                    ld.IsActive = true;
                    return ld;
                }
            }

            var doc = new LayoutDocument
            {
                Title     = title,
                ContentId = contentId,
                Content   = content,
                CanClose  = true,
            };
            _mapPane.Children.Add(doc);
            doc.IsActive = true;
            return doc;
        }

        public void RemoveDocument(string contentId)
        {
            for (int i = _mapPane.Children.Count - 1; i >= 0; i--)
            {
                if (_mapPane.Children[i] is LayoutDocument ld && ld.ContentId == contentId)
                {
                    _mapPane.Children.RemoveAt(i);
                    break;
                }
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void RestoreLayout(NavigationView navView)
        {
            if (_settings.WorkspaceLayout == null) return;
            try
            {
                var xdoc = XDocument.Parse(_settings.WorkspaceLayout);

                // Locate the Navigator element anywhere in the document.
                var navEl = xdoc.Descendants("LayoutAnchorable")
                    .FirstOrDefault(el =>
                        el.Attribute("ContentId")?.Value == "Navigator" ||
                        el.Attribute("Title")?.Value     == "Navigator");

                if (navEl == null)
                {
                    EdnaLogger.Warn("Saved layout missing Navigator -- discarding.");
                    _settings.WorkspaceLayout = null;
                    return;
                }

                // If it landed in <Hidden> (auto-hide rail), move it back to its previous
                // pane in the XML before deserializing -- fixing the data rather than
                // fighting AvalonDock runtime state after the fact.
                if (navEl.Parent?.Name.LocalName == "Hidden")
                {
                    var prevId = navEl.Attribute("PreviousContainerId")?.Value;
                    var target = xdoc.Descendants("LayoutAnchorablePane")
                                     .FirstOrDefault(el => el.Attribute("Id")?.Value == prevId);
                    if (target != null)
                    {
                        navEl.Remove();
                        target.AddFirst(navEl);
                    }
                }

                // Strip saved document tabs -- documents are recreated on demand by their skills.
                xdoc.Descendants("LayoutDocument").Remove();

                var serializer = new XmlLayoutSerializer(DockManager);
                serializer.LayoutSerializationCallback += (s, e) =>
                {
                    if (e.Model is LayoutAnchorable la &&
                        (la.ContentId == "Navigator" || la.Title == "Navigator"))
                    {
                        e.Content    = navView;
                        la.ContentId = "Navigator";
                    }
                };
                using (var sr = new StringReader(xdoc.ToString()))
                    serializer.Deserialize(sr);

                // Deserialization replaced the layout tree -- re-point our references.
                _navPane = DockManager.Layout.Descendents()
                               .OfType<LayoutAnchorable>()
                               .FirstOrDefault(a => a.ContentId == "Navigator") ?? _navPane;

                _mapPane = DockManager.Layout.Descendents()
                               .OfType<LayoutDocumentPane>()
                               .FirstOrDefault() ?? _mapPane;

                // Safety net: guarantee the Navigator always has content.
                if (_navPane.Content == null)
                    _navPane.Content = navView;
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"Layout restore failed: {ex.Message}");
                _settings.WorkspaceLayout = null;
            }
        }

        internal void SaveAndHide()
        {
            _settings.WorkspaceBounds = new Rect(Left, Top, Width, Height);

            try
            {
                var serializer = new XmlLayoutSerializer(DockManager);
                using var sw = new StringWriter();
                serializer.Serialize(sw);
                _settings.WorkspaceLayout = sw.ToString();
            }
            catch (Exception ex) { EdnaLogger.Warn($"Layout save failed: {ex.Message}"); }

            _settings.Save();
            Hide();
        }

        internal void ForceClose()
        {
            Closing -= _closingHandler;
            Close();
        }
    }
}
