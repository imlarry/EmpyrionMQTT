using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using EDNAClient.Configuration;
using EDNAClient.Core;
using EDNAClient.Helpers;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace EDNAClient.Workspace
{
    public partial class WorkspaceWindow : Window, ISkillWorkspace
    {
        public Window DialogOwner => this;

        private readonly EdnaSettings         _settings;
        private readonly NavigationViewModel  _navViewModel;
        private readonly List<IDockableSkill> _registeredSkills = new();
        private readonly List<IDocumentSkill> _documentSkills   = new();

        private readonly Dictionary<string, List<DocumentMenuAction>> _documentMenuItems = new();
        private string? _activeContentId;

        private WorkspaceState _state;

        // Live references into the active layout tree.
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
            SetupDocumentMenus();

            _navPane = NavPane;
            _mapPane = MapPane;

            _state = WorkspaceState.Load(WellKnownPaths.WorkspaceStateFile);
            _state.ApplyBounds(this);

            var navView = new NavigationView(_navViewModel);
            _navPane.Content = navView;

            RestoreLayout(navView);

            // Re-apply expansion state whenever a new root section is added by a skill.
            _navViewModel.RootNodes.CollectionChanged += OnNavRootsChanged;

            _closingHandler = (s, e) => { e.Cancel = true; Application.Current.Shutdown(); };
            Closing += _closingHandler;
        }

        private void OnNavRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && _state.ExpandedNav.Count > 0)
            {
                EdnaLogger.Log($"[Workspace] nav section added -- applying {_state.ExpandedNav.Count} saved expansion paths");
                _navViewModel.ApplyExpandedPaths(_state.ExpandedNav);
            }
        }

        // ── Document skill registry ───────────────────────────────────────────

        // Skills with documents register so SaveAndHide can query them.
        public void RegisterDocumentSkill(IDocumentSkill skill)
        {
            if (!_documentSkills.Contains(skill))
                _documentSkills.Add(skill);
        }

        public void UnregisterDocumentSkill(IDocumentSkill skill) =>
            _documentSkills.Remove(skill);

        // Returns the saved document IDs for a given skill (from last session).
        public IReadOnlyList<string> GetSavedDocuments(string skillId)
        {
            if (_state.OpenDocuments.TryGetValue(skillId, out var list) && list.Count > 0)
            {
                EdnaLogger.Log($"[Workspace] {list.Count} saved doc IDs for '{skillId}'");
                return list;
            }
            return Array.Empty<string>();
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

        // ── Document menus ────────────────────────────────────────────────────

        public void SetDocumentMenuItems(string contentId, IEnumerable<DocumentMenuAction> items)
        {
            _documentMenuItems[contentId] = items.ToList();
        }

        private void SetupDocumentMenus()
        {
            DockManager.ActiveContentChanged += (s, e) =>
            {
                var doc = DockManager.Layout.Descendents()
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(d => d.IsActive);
                if (doc != null)
                    _activeContentId = doc.ContentId;
            };

            var menu = new ContextMenu();
            menu.Opened += OnDocumentContextMenuOpened;
            DockManager.DocumentContextMenu = menu;

            DockManager.DocumentPaneMenuItemHeaderTemplate =
                (DataTemplate)Resources["DocPickerItemTemplate"];
        }

        // Walks the visual (then logical) tree from a starting element to find
        // an ancestor of the given type.
        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current)
                       ?? LogicalTreeHelper.GetParent(current) as DependencyObject;
            }
            return null;
        }

        private void OnDocumentContextMenuOpened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            menu.Items.Clear();

            // Prefer the tab that was right-clicked over the keyboard-active document.
            string? contentId = null;
            if (menu.PlacementTarget is DependencyObject target)
            {
                var tabItem = FindAncestor<LayoutDocumentTabItem>(target);
                contentId = (tabItem?.Model as LayoutDocument)?.ContentId;
            }
            contentId = contentId ?? _activeContentId;

            // Skill-provided file-operation items at the top.
            if (contentId != null &&
                _documentMenuItems.TryGetValue(contentId, out var skillItems) &&
                skillItems.Count > 0)
            {
                foreach (var action in skillItems)
                    menu.Items.Add(new MenuItem
                    {
                        Header  = action.Header,
                        Command = new SimpleCommand(action.Execute),
                    });
                menu.Items.Add(new Separator());
            }

            // Standard AvalonDock layout items.
            var doc        = DockManager.Layout.Descendents()
                                 .OfType<LayoutDocument>()
                                 .FirstOrDefault(d => d.ContentId == contentId);
            var layoutItem = doc != null ? DockManager.GetLayoutItemFromModel(doc) : null;

            menu.Items.Add(new MenuItem { Header = "Float",                   Command = layoutItem?.FloatCommand });
            menu.Items.Add(new MenuItem { Header = "Dock as Tabbed Document", Command = layoutItem?.DockAsDocumentCommand });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Close",                   Command = layoutItem?.CloseCommand });
            menu.Items.Add(new MenuItem { Header = "Close All But This",      Command = layoutItem?.CloseAllButThisCommand });
            menu.Items.Add(new MenuItem { Header = "Close All",               Command = layoutItem?.CloseAllCommand });
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
            // Reactivate if already open anywhere in the layout (docked or floating).
            var existing = DockManager.Layout.Descendents()
                .OfType<LayoutDocument>()
                .FirstOrDefault(d => d.ContentId == contentId);
            if (existing != null)
            {
                existing.IsActive = true;
                return existing;
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
            EdnaLogger.Log($"[Workspace] opened document '{contentId}'");
            return doc;
        }

        // Force-removes a document from anywhere in the layout (docked or floating)
        // without triggering the Closing event. Used by skill cleanup paths.
        public void RemoveDocument(string contentId)
        {
            var doc = DockManager.Layout.Descendents()
                .OfType<LayoutDocument>()
                .FirstOrDefault(d => d.ContentId == contentId);

            if (doc?.Parent is ILayoutContainer parent)
            {
                parent.RemoveChild(doc);
                EdnaLogger.Log($"[Workspace] removed document '{contentId}'");
            }
            _documentMenuItems.Remove(contentId);
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void RestoreLayout(NavigationView navView)
        {
            if (_state.LayoutXml == null)
            {
                EdnaLogger.Log("[Workspace] no saved layout -- using default XAML layout");
                return;
            }

            try
            {
                EdnaLogger.Log("[Workspace] restoring dock layout from saved state");
                var xdoc = XDocument.Parse(_state.LayoutXml);

                var navEl = xdoc.Descendants("LayoutAnchorable")
                    .FirstOrDefault(el =>
                        el.Attribute("ContentId")?.Value == "Navigator" ||
                        el.Attribute("Title")?.Value     == "Navigator");

                if (navEl == null)
                {
                    EdnaLogger.Warn("[Workspace] saved layout missing Navigator -- discarding");
                    _state.LayoutXml = null;
                    return;
                }

                if (navEl.Parent?.Name.LocalName == "Hidden")
                {
                    var prevId = navEl.Attribute("PreviousContainerId")?.Value;
                    var target = xdoc.Descendants("LayoutAnchorablePane")
                                     .FirstOrDefault(el => el.Attribute("Id")?.Value == prevId);
                    if (target != null)
                    {
                        navEl.Remove();
                        target.AddFirst(navEl);
                        EdnaLogger.Log("[Workspace] moved Navigator out of Hidden back into its pane");
                    }
                }

                // Strip all document content -- floating window shells and docked tabs.
                // Documents are recreated on demand by RestoreDocuments after each GameEnter.
                int removedFloating = xdoc.Descendants("LayoutDocumentFloatingWindow").Count();
                xdoc.Descendants("LayoutDocumentFloatingWindow").Remove();
                int removedDocked = xdoc.Descendants("LayoutDocument").Count();
                xdoc.Descendants("LayoutDocument").Remove();
                EdnaLogger.Log($"[Workspace] stripped {removedDocked} docked + {removedFloating} floating document entries from saved layout");

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

                _navPane = DockManager.Layout.Descendents()
                               .OfType<LayoutAnchorable>()
                               .FirstOrDefault(a => a.ContentId == "Navigator") ?? _navPane;

                _mapPane = DockManager.Layout.Descendents()
                               .OfType<LayoutDocumentPane>()
                               .FirstOrDefault() ?? _mapPane;

                if (_navPane.Content == null)
                    _navPane.Content = navView;

                EdnaLogger.Log("[Workspace] dock layout restored successfully");
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"[Workspace] layout restore failed: {ex.Message}");
                _state.LayoutXml = null;
            }
        }

        // Captures full workspace state and saves to disk. Window stays visible.
        // Must be called while skills are still alive (nav/docs still present).
        internal void SaveState()
        {
            _state.CaptureBounds(this);

            _state.OpenDocuments = new Dictionary<string, List<string>>();
            foreach (var skill in _documentSkills)
            {
                var ids = skill.GetOpenDocumentIds();
                if (ids.Count > 0)
                    _state.OpenDocuments[skill.Id] = ids.ToList();
            }

            _state.ExpandedNav = _navViewModel.CollectExpandedPaths();

            EdnaLogger.Log($"[Workspace] saving state: bounds={_state.Left:F0},{_state.Top:F0} docs={_state.OpenDocuments.Values.Sum(v => v.Count)} navPaths={_state.ExpandedNav.Count}");

            try
            {
                var serializer = new XmlLayoutSerializer(DockManager);
                using var sw = new StringWriter();
                serializer.Serialize(sw);
                _state.LayoutXml = sw.ToString();
                EdnaLogger.Log("[Workspace] dock layout serialized");
            }
            catch (Exception ex)
            {
                EdnaLogger.Warn($"[Workspace] layout serialize failed: {ex.Message}");
            }

            _state.Save(WellKnownPaths.WorkspaceStateFile);
        }

        internal void ForceClose()
        {
            SaveState();
            Closing -= _closingHandler;
            Close();
        }
    }
}
