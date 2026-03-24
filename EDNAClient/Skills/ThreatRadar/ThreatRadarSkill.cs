using System.Threading.Tasks;
using EDNAClient.Core;
using ESB.Messaging;

namespace EDNAClient.Skills.ThreatRadar
{
    public class ThreatRadarSkill : IEdnaSkill
    {
        private readonly ThreatViewModel _viewModel;
        private readonly ThreatWindow    _window;
        private ThreatTracker?           _tracker;

        public string Id => "ThreatRadar";

        public ThreatRadarSkill(ThreatViewModel viewModel)
        {
            _viewModel = viewModel;
            _window    = new ThreatWindow(viewModel);
        }

        public async Task StartAsync(IMessenger messenger)
        {
            _tracker = new ThreatTracker(messenger, _viewModel);
            await _tracker.StartAsync();
        }

        public void Stop()
        {
            _tracker?.Stop();
            _tracker = null;
            if (_window.IsVisible) _window.Hide();
        }

        public void SnapToGameWindow()
        {
            if (_window.IsVisible) _window.SnapToGameWindow();
        }
    }
}
