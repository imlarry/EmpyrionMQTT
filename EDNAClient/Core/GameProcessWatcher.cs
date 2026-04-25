using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EDNAClient.Core
{
    public class GameProcessWatcher : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private Timer?   _pollTimer;
        private Process? _gameProcess;
        private bool     _disposed;
        private DateTime _lastExitUtc = DateTime.MinValue;

        public event Action? GameStarted;
        public event Action? GameExited;

        public void Start()
        {
            if (TryAttach())
            {
                GameStarted?.Invoke();
                return;
            }
            EdnaLogger.Log("Game window not found -- polling every 2s");
            _pollTimer = new Timer(_ => Poll(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        private void Poll()
        {
            if (_disposed) return;
            if (TryAttach())
            {
                _pollTimer?.Dispose();
                _pollTimer = null;
                GameStarted?.Invoke();
            }
        }

        private bool TryAttach()
        {
            var hwnd = GameWindowLocator.GetWindowHandle();
            if (hwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;

            try
            {
                var proc = Process.GetProcessById((int)pid);

                // In coop mode, playfield server processes share the same exe name and window title
                // as the client. Reject any process that was already running when the last session
                // ended -- those are lingering server processes, not a new game session.
                if (_lastExitUtc != DateTime.MinValue && proc.StartTime.ToUniversalTime() < _lastExitUtc)
                {
                    EdnaLogger.Log($"Skipping {proc.ProcessName} (PID {pid}) -- started before last session exit");
                    proc.Dispose();
                    return false;
                }

                _gameProcess = proc;
                _gameProcess.EnableRaisingEvents = true;
                _gameProcess.Exited += OnProcessExited;
                EdnaLogger.Log($"Attached to {_gameProcess.ProcessName} (PID {pid})");
                return true;
            }
            catch (ArgumentException)
            {
                EdnaLogger.Warn($"PID {pid} exited between FindWindow and GetProcessById -- skipping");
                return false;
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            _lastExitUtc = DateTime.UtcNow;
            _gameProcess?.Dispose();
            _gameProcess = null;
            GameExited?.Invoke();
        }

        public void Dispose()
        {
            _disposed = true;
            _pollTimer?.Dispose();
            _pollTimer = null;
            _gameProcess?.Dispose();
            _gameProcess = null;
        }
    }
}
