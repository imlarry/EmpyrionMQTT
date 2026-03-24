using System.Windows;
using WinFormsApp = System.Windows.Forms.Application;
using EDNAClient.Core;
using EDNAClient.Skills.StatusPill;
using EDNAClient.Skills.ThreatRadar;
using EDNAClient.Tray;
using EDNAClient.Settings;

namespace EDNAClient
{
    public partial class App : Application
    {
        private EdnaService?     _service;
        private TrayIconManager? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // WPF apps must opt out of automatic shutdown when the last window closes,
            // since EDNA runs as a tray-only app until the game is detected.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Enable WinForms visual styles so the context menu renders correctly.
            WinFormsApp.EnableVisualStyles();

            var settings    = EdnaSettings.Load();
            var pill        = new StatusPillSkill(new HudViewModel());
            var threatRadar = new ThreatRadarSkill(new ThreatViewModel());

            _tray = new TrayIconManager(() =>
            {
                var dlg = new SettingsWindow(settings);
                dlg.ShowDialog();
            });

            _service = new EdnaService(
                skills:   new IEdnaSkill[] { pill, threatRadar },
                pill:     pill,
                tray:     _tray,
                settings: settings);
            _service.Start();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_service != null)
                await _service.StopAsync();
            _tray?.Dispose();
            base.OnExit(e);
        }
    }
}
