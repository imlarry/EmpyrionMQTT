using System.Threading.Tasks;
using System.Windows;
using EDNAClient.Core;
using ESB.Messaging;

namespace EDNAClient.Skills.StatusPill
{
    public class StatusPillSkill : IEdnaSkill
    {
        private readonly HudViewModel _viewModel;
        private readonly HudWindow    _window;

        public string Id => "StatusPill";

        public HudWindow Window => _window;

        public StatusPillSkill(HudViewModel viewModel)
        {
            _viewModel = viewModel;
            _window    = new HudWindow(viewModel);
        }

        public Task StartAsync(IMessenger messenger)
        {
            SnapToGameWindow();
            _window.Show();
            return Task.CompletedTask;
        }

        public void Stop() => _window.Hide();

        public void SnapToGameWindow() => _window.SnapToGameWindow();

        public void UpdateIndicator(IndicatorState state) => _viewModel.IndicatorState = state;
    }
}
