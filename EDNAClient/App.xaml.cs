using System.Threading;
using System.Windows;
using WinFormsApp = System.Windows.Forms.Application;
using EDNAClient.Core;
using EDNAClient.Skills.FloorMap;
using EDNAClient.Skills.ThreatRadar;
using EDNAClient.Tray;
using EDNAClient.Settings;

namespace EDNAClient
{
    public partial class App : Application
    {
        private EdnaService?     _service;
        private TrayIconManager? _tray;
        private Mutex?           _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _instanceMutex = new Mutex(true, "Local\\EDNA_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _instanceMutex.Dispose();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // WPF apps must opt out of automatic shutdown when the last window closes,
            // since EDNA runs as a tray-only app until the game is detected.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Enable WinForms visual styles so the context menu renders correctly.
            WinFormsApp.EnableVisualStyles();

            var settings    = EdnaSettings.Load();
            var threatRadar = new ThreatRadarSkill(new ThreatViewModel());
            var floorMap    = new FloorMapSkill(new FloorMapViewModel());

            _tray = new TrayIconManager(() =>
            {
                var dlg = new SettingsWindow(settings);
                dlg.ShowDialog();
            });

            _service = new EdnaService(
                skills:   new IEdnaSkill[] { threatRadar, floorMap },
                tray:     _tray,
                settings: settings);
            _service.Start();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_service != null)
                await _service.StopAsync();
            _tray?.Dispose();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
