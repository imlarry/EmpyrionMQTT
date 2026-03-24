using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDNAClient.Skills.ThreatRadar
{
    public enum ThreatLevel { None, Near, Close, Imminent }
    // None     = >100 m or no predator
    // Near     = 100–50 m  → slow pulse (1 Hz)
    // Close    = 50–20 m   → fast pulse (3 Hz)
    // Imminent = <20 m     → solid

    public class ThreatViewModel : INotifyPropertyChanged
    {
        private ThreatLevel _north;
        private ThreatLevel _east;
        private ThreatLevel _south;
        private ThreatLevel _west;

        public ThreatLevel NorthThreat
        {
            get => _north;
            set { _north = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThreats)); }
        }

        public ThreatLevel EastThreat
        {
            get => _east;
            set { _east  = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThreats)); }
        }

        public ThreatLevel SouthThreat
        {
            get => _south;
            set { _south = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThreats)); }
        }

        public ThreatLevel WestThreat
        {
            get => _west;
            set { _west  = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThreats)); }
        }

        public bool HasThreats =>
            _north != ThreatLevel.None || _east  != ThreatLevel.None ||
            _south != ThreatLevel.None || _west  != ThreatLevel.None;

        public void ClearAll()
        {
            NorthThreat = ThreatLevel.None;
            EastThreat  = ThreatLevel.None;
            SouthThreat = ThreatLevel.None;
            WestThreat  = ThreatLevel.None;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
