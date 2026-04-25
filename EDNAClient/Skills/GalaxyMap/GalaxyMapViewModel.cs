using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDNAClient.Skills.GalaxyMap
{
    internal enum GalaxyFilter { All, Near30LY }

    internal sealed class GalaxyMapViewModel : INotifyPropertyChanged
    {
        private List<StarSystem> _all = new List<StarSystem>();
        private double _px, _py, _pz;
        private GalaxyFilter _filter = GalaxyFilter.All;
        private StarSystem? _selected;
        private StarSystem[] _visibleSystems = Array.Empty<StarSystem>();

        public StarSystem[] VisibleSystems
        {
            get => _visibleSystems;
            private set { _visibleSystems = value; OnPropertyChanged(); }
        }

        public string StatusText =>
            _selected != null
                ? $"{_selected.Name}  ({_selected.X}, {_selected.Y}, {_selected.Z})"
                : "Click a star to select";

        public void LoadSystems(List<StarSystem> systems)
        {
            _all = systems;
            Rebuild();
        }

        public void SetPlayerPosition(double x, double y, double z)
        {
            _px = x; _py = y; _pz = z;
            HasPlayerPosition = true;
            Rebuild();
        }

        public void SetFilter(GalaxyFilter filter)
        {
            _filter = filter;
            Rebuild();
        }

        public void SetSelected(StarSystem s)
        {
            _selected = s;
            OnPropertyChanged(nameof(StatusText));
        }

        public bool HasPlayerPosition { get; private set; }

        public StarSystem[] GetNearbyStars(double radius)
        {
            if (!HasPlayerPosition || _all.Count == 0) return Array.Empty<StarSystem>();
            return _all.Where(s => s.DistanceTo(_px, _py, _pz) <= radius).ToArray();
        }

        private void Rebuild()
        {
            VisibleSystems = _filter == GalaxyFilter.All
                ? _all.ToArray()
                : _all.Where(s => s.DistanceTo(_px, _py, _pz) <= 30.0).ToArray();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
