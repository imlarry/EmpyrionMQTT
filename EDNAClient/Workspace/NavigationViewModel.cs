using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDNAClient.Workspace
{
    public enum NavNodeType { Galaxy, GalaxyFilter, System, Planet, Structure, Floor, ScriptFolder, ScriptFile,
                              MapRoot, MapSolarSystem, MapPlayfield, MapEntity, MapFloor,
                              SavesRoot, SaveFolder, SaveFile }

    public class NavMenuItem
    {
        public string             Header      { get; set; } = "";
        public Action             Execute     { get; set; } = () => { };
        public bool               IsSeparator { get; set; }
        public List<NavMenuItem>? SubItems    { get; set; }

        public static NavMenuItem Separator() => new NavMenuItem { IsSeparator = true };
    }

    public class NavNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string      Name     { get; set; } = "";
        public NavNodeType NodeType { get; set; }
        public object?     Tag      { get; set; }

        public ObservableCollection<NavNode> Children { get; } = new();

        public Action?            OnSelected   { get; set; }
        public List<NavMenuItem>? ContextItems { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NavigationViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<NavNode> RootNodes { get; } = new();

        public void AddRootSection(NavNode node)
        {
            RemoveRootSection(node.Name);
            RootNodes.Add(node);
        }

        public void RemoveRootSection(string name)
        {
            for (int i = RootNodes.Count - 1; i >= 0; i--)
                if (RootNodes[i].Name == name)
                    RootNodes.RemoveAt(i);
        }

        // Returns "Root/Child/GrandChild" paths for every currently-expanded node.
        public List<string> CollectExpandedPaths()
        {
            var result = new List<string>();
            foreach (var root in RootNodes)
                CollectExpanded(root, root.Name, result);
            return result;
        }

        // Walks the current tree and expands any node whose path is in the saved set.
        public void ApplyExpandedPaths(IEnumerable<string> paths)
        {
            var set = new HashSet<string>(paths, StringComparer.Ordinal);
            foreach (var root in RootNodes)
                ApplyExpanded(root, root.Name, set);
        }

        private static void CollectExpanded(NavNode node, string path, List<string> result)
        {
            if (node.IsExpanded) result.Add(path);
            foreach (var child in node.Children)
                CollectExpanded(child, path + "/" + child.Name, result);
        }

        private static void ApplyExpanded(NavNode node, string path, HashSet<string> expanded)
        {
            node.IsExpanded = expanded.Contains(path);
            foreach (var child in node.Children)
                ApplyExpanded(child, path + "/" + child.Name, expanded);
        }

        /// <summary>
        /// Called by map skills to upsert the Galaxy > System > Planet > Structure > Floor
        /// hierarchy. Designed additively: skills extend the tree without replacing existing nodes.
        /// </summary>
        public void UpdateStructureContext(
            string   galaxy,
            string   system,
            string   planet,
            string   structure,
            string[] floors)
        {
            // stub -- full implementation added when LandMap/SystemMap skills are built.
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
