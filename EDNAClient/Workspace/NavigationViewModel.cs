using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDNAClient.Workspace
{
    public enum NavNodeType { Galaxy, System, Planet, Structure, Floor, ScriptFolder, ScriptFile }

    public class NavMenuItem
    {
        public string Header      { get; set; } = "";
        public Action Execute     { get; set; } = () => { };
        public bool   IsSeparator { get; set; }

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

        public NavigationViewModel()
        {
            RootNodes.Add(new NavNode
            {
                Name       = "Galaxy",
                NodeType   = NavNodeType.Galaxy,
                IsExpanded = false,
            });
        }

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
