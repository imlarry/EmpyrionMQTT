using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using EDNAClient.Core;

namespace EDNAClient.Skills.StatusPill
{
    public class HudViewModel : INotifyPropertyChanged
    {
        private IndicatorState _indicatorState = IndicatorState.Offline;

        public IndicatorState IndicatorState
        {
            get => _indicatorState;
            set
            {
                _indicatorState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IndicatorBrush));
            }
        }

        public Brush IndicatorBrush => _indicatorState switch
        {
            IndicatorState.Healthy => ResourceBrush("IndicatorHealthy"),
            IndicatorState.Warning => ResourceBrush("IndicatorWarning"),
            IndicatorState.Error   => ResourceBrush("IndicatorError"),
            _                      => ResourceBrush("IndicatorOffline")
        };

        private static Brush ResourceBrush(string key) =>
            Application.Current.Resources[key] as Brush ?? Brushes.Gray;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
