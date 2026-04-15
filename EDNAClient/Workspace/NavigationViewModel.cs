using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDNAClient.Workspace
{
    public enum NavNodeType { Galaxy, System, Planet, Structure, Floor }

    public class NavNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string      Name     { get; set; } = "";
        public NavNodeType NodeType { get; set; }

        public ObservableCollection<NavNode> Children { get; } = new();

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
            // Stub root -- replaced by live data as skills publish context.
            // Future skills call UpdateStructureContext() to populate the tree.
            RootNodes.Add(new NavNode
            {
                Name        = "Local Galaxy",
                NodeType    = NavNodeType.Galaxy,
                IsExpanded  = true,
            });
        }

        /// <summary>
        /// Called by map skills to upsert the Galaxy > System > Planet > Structure > Floor
        /// hierarchy and select the active floor node. Designed additively: skills extend
        /// the tree without replacing existing nodes.
        /// </summary>
        public void UpdateStructureContext(
            string   galaxy,
            string   system,
            string   planet,
            string   structure,
            string[] floors)
        {
            // Stub -- full implementation added when LandMap/SystemMap skills are built.
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
