using EDNAClient.Core;
using ESB.Messaging;
using System.Windows;

namespace EDNAClient.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly EdnaSettings _settings;

        public SettingsWindow(EdnaSettings settings)
        {
            InitializeComponent();
            _settings = settings;

            var esbInfo = WellKnownPaths.LoadEsbInfo();
            HostLabel.Text = esbInfo?.MQTThost?.WithTcpServer ?? "localhost";
            PortLabel.Text = esbInfo?.MQTThost?.Port.ToString() ?? "1883";

            StartupCheck.IsChecked = settings.GetRunAtStartup();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _settings.Save();
            _settings.SetRunAtStartup(StartupCheck.IsChecked == true);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
