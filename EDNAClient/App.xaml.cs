using System.Windows;
using EDNAClient.Core;
using EDNAClient.ViewModels;

namespace EDNAClient
{
    public partial class App : Application
    {
        private EdnaService? _service;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var viewModel = new HudViewModel();
            var window = new HudWindow(viewModel);
            window.Show();

            _service = new EdnaService(viewModel);
            try
            {
                await _service.StartAsync();
                viewModel.BrokerConnected = true;
            }
            catch (Exception ex)
            {
                viewModel.StatusDetail = ex.Message;
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_service != null)
                await _service.StopAsync();
            base.OnExit(e);
        }
    }
}
