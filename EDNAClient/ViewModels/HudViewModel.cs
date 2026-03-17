using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EDNAClient.ViewModels
{
    public class HudViewModel : INotifyPropertyChanged
    {
        private bool _brokerConnected;
        private string? _gameName;
        private string? _gameMode;
        private string? _statusDetail;
        private int? _entityCount;

        public bool BrokerConnected
        {
            get => _brokerConnected;
            set { _brokerConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(BrokerStatus)); OnPropertyChanged(nameof(BrokerStatusColor)); }
        }

        public string? GameName
        {
            get => _gameName;
            set { _gameName = value; OnPropertyChanged(); OnPropertyChanged(nameof(GameStatus)); }
        }

        public string? GameMode
        {
            get => _gameMode;
            set { _gameMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(GameStatus)); }
        }

        public string? StatusDetail
        {
            get => _statusDetail;
            set { _statusDetail = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDetailVisibility)); }
        }

        public int? EntityCount
        {
            get => _entityCount;
            set { _entityCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(GameStatus)); }
        }

        public string BrokerStatus => BrokerConnected ? "Broker connected" : "Broker disconnected";
        public string BrokerStatusColor => BrokerConnected ? "#FF00FF88" : "#FFFF4444";
        public string GameStatus => GameName != null
            ? $"{GameName}  [{GameMode}]{(EntityCount.HasValue ? $"  {EntityCount} entities" : "")}"
            : "No game active";
        public Visibility StatusDetailVisibility => string.IsNullOrEmpty(StatusDetail) ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
