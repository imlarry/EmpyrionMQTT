using System.Windows;
using WinFormsApp = System.Windows.Forms.Application;
using EDNAClient.Configuration;
using EDNAClient.Core;
using EDNAClient.Skills.FloorMap;
using EDNAClient.Skills.Scripting.ScriptEditor;
using EDNAClient.Skills.GalaxyMap;
using EDNAClient.Skills.ThreatRadar;
using EDNAClient.Tray;
using EDNAClient.Workspace;

namespace EDNAClient
{
    public partial class App : Application
    {
        private EdnaService?      _service;
        private TrayIconManager?  _tray;
        private Mutex?            _instanceMutex;
        private WorkspaceWindow?  _workspace;

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

            EdnaLogger.Init(WellKnownPaths.LogsDirectory);
            EdnaLogger.Log("EDNA starting");

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            WinFormsApp.EnableVisualStyles();

            _tray      = new TrayIconManager();
            _workspace = new WorkspaceWindow(EdnaSettings.Load());
            _workspace.Show();

            var threatRadar  = new ThreatRadarSkill(new ThreatViewModel());
            var floorMap     = new FloorMapSkill(_workspace);
            var scriptEditor = new ScriptEditorSkill(_workspace);
            var galaxyMap    = new GalaxyMapSkill(_workspace);

            _service = new EdnaService(
                skills:    new IEdnaSkill[] { threatRadar, floorMap, scriptEditor, galaxyMap },
                tray:      _tray,
                workspace: _workspace);

            _ = _service.StartAsync();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            EdnaLogger.Log("EDNA stopping");
            if (_service != null)
                await _service.StopAsync();
            _workspace?.ForceClose();
            _tray?.Dispose();
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
            EdnaLogger.Close();
            base.OnExit(e);
        }
    }
}
