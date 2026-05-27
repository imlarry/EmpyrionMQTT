using System.Windows;
using System.Windows.Threading;
using WinFormsApp = System.Windows.Forms.Application;
using EDNAClient.Configuration;
using EDNAClient.Core;
using EDNAClient.Core.ShapeBake;
using EDNAClient.Skills.FloorMap;
using EDNAClient.Skills.Scripting.ScriptEditor;
using EDNAClient.Skills.GalaxyMap;
using EDNAClient.Skills.ThreatRadar;
using EDNAClient.Skills.Tomography;
using EDNAClient.Skills.WindowToggle;
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

            // Last-chance handlers: log full stack before EDNA exits. Hooked
            // right after logger init so nothing between here and Shutdown can
            // die silently. Detached-process model means EDNA's window is the
            // only place these surface -- without logging, a crash leaves no
            // trace in either EDNA's or Empyrion's logs.
            HookUnhandledExceptionLogging();

            // Spike: load BlocksConfig.ecf and log a summary so we can verify the
            // parser works end-to-end against the user's Empyrion install.
            if (BlocksConfig.TryLoadFromEmpyrionInstall(out var blocksErr))
                EdnaLogger.Log(BlocksConfig.Summarize());
            else
                EdnaLogger.Warn("BlocksConfig load skipped: " + blocksErr);

            if (BlockShapeCategories.TryLoadFromEmpyrionInstall(out var catErr))
                EdnaLogger.Log(BlockShapeCategories.Summarize());
            else
                EdnaLogger.Warn("BlockShapeCategories load skipped: " + catErr);

            if (ShapeStampCatalog.TryLoadFromBaseDirectory(out var stampErr))
                EdnaLogger.Log(ShapeStampCatalog.Summarize());
            else
                EdnaLogger.Warn("ShapeStampCatalog load skipped: " + stampErr);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            WinFormsApp.EnableVisualStyles();

            _tray      = new TrayIconManager();
            _workspace = new WorkspaceWindow(EdnaSettings.Load());
            _workspace.Show();

            var threatRadar  = new ThreatRadarSkill(new ThreatViewModel());
            var floorMap     = new FloorMapSkill(_workspace);
            var tomography   = new TomographySkill(_workspace);
            var scriptEditor = new ScriptEditorSkill(_workspace);
            var galaxyMap    = new GalaxyMapSkill(_workspace);
            var windowToggle = new WindowToggleSkill(_workspace);

            _service = new EdnaService(
                skills:    new IEdnaSkill[] { threatRadar, floorMap, tomography, scriptEditor, galaxyMap, windowToggle },
                tray:      _tray,
                workspace: _workspace);

            _ = _service.StartAsync();
        }

        private void HookUnhandledExceptionLogging()
        {
            // UI-thread exceptions go through the WPF dispatcher. Leave
            // Handled=false so the process still dies after logging -- existing
            // failure semantics are preserved, we just no longer lose the
            // stack to /dev/null.
            DispatcherUnhandledException += (s, args) =>
            {
                LogFatal("DispatcherUnhandledException", args.Exception);
            };

            // Non-UI thread crashes (e.g. background Task.Run that swallows
            // exceptions only to rethrow on a finalizer thread, or unhandled
            // exceptions from raw threads). IsTerminating is almost always
            // true here -- the process is dying regardless.
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogFatal(
                    args.IsTerminating ? "AppDomain.UnhandledException (terminating)"
                                       : "AppDomain.UnhandledException",
                    args.ExceptionObject as Exception);
            };

            // Exceptions from Tasks that were never awaited / never had their
            // .Exception observed. SetObserved keeps the process alive so a
            // forgotten background task can't kill EDNA -- we just want a record.
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                LogFatal("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };
        }

        private static void LogFatal(string source, Exception? ex)
        {
            try
            {
                if (ex == null)
                {
                    EdnaLogger.Error($"{source}: (no exception details)");
                    return;
                }
                // ex.ToString() includes type, message, stack, and the full
                // chain of inner exceptions. Embedded newlines land in the log
                // file as-is; the timestamp on the first line is enough to
                // anchor the block when grepping.
                EdnaLogger.Error($"{source}\n{ex}");
            }
            catch
            {
                // Logging itself failed -- nothing we can do without
                // making the original exception worse.
            }
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
