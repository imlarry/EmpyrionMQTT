using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using EDNAClient.Core;
using EDNAClient.Helpers;
using EDNAClient.Workspace;

namespace EDNAClient.Skills.WindowToggle
{
    // Registers Ctrl+Shift+R as a global focus-toggle between the EDNA workspace
    // window and the Empyrion game window.
    //
    // This exists because Empyrion's input system locks input focus to its own
    // window: clicking elsewhere does not always bring that window forward, and
    // moving back requires the Windows key. A global Win32 hotkey is the only
    // reliable way to swap focus without leaving the keyboard.
    public class WindowToggleSkill : IEdnaSkill, IHotkeyProvider
    {
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private readonly Window _workspace;

        // Last known cursor position in EDNA-window client coords, captured when
        // toggling out to the game. Restored (or defaulted to 20,20) when toggling back.
        private POINT? _savedEdnaClientPos;

        public string Id    => "WindowToggle";
        public string Title => "Window Toggle";

        public WindowToggleSkill(Window workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        public Task StartAsync(EdnaContext ctx) => Task.CompletedTask;
        public void Stop() { }
        public void SnapToGameWindow() { }

        public IEnumerable<HotkeyRequest> GetHotkeyRequests()
        {
            yield return new HotkeyRequest(
                HotkeyRequest.ModControl | HotkeyRequest.ModShift | HotkeyRequest.NoRepeat,
                0x52,   // VK_R
                OnToggleHotkey);
        }

        private void OnToggleHotkey()
        {
            UI.Invoke(() =>
            {
                IntPtr ednaHwnd = new WindowInteropHelper(_workspace).Handle;
                IntPtr gameHwnd = GameWindowLocator.GetWindowHandle();
                IntPtr fg       = GetForegroundWindow();

                // If EDNA is the foreground window, swap to the game (when present).
                // Otherwise bring EDNA forward.
                if (fg == ednaHwnd && gameHwnd != IntPtr.Zero)
                {
                    // Capture mouse position in EDNA-client coords so we can restore it on return.
                    if (ednaHwnd != IntPtr.Zero && GetCursorPos(out var screenPt))
                    {
                        var p = screenPt;
                        if (ScreenToClient(ednaHwnd, ref p)) _savedEdnaClientPos = p;
                    }

                    if (IsIconic(gameHwnd)) ShowWindow(gameHwnd, SW_RESTORE);
                    SetForegroundWindow(gameHwnd);
                    EdnaLogger.Detail("[WindowToggle] -> game");
                }
                else
                {
                    if (_workspace.Visibility != Visibility.Visible) _workspace.Show();
                    if (_workspace.WindowState == WindowState.Minimized) _workspace.WindowState = WindowState.Normal;
                    if (ednaHwnd != IntPtr.Zero && IsIconic(ednaHwnd)) ShowWindow(ednaHwnd, SW_RESTORE);
                    _workspace.Activate();
                    if (ednaHwnd != IntPtr.Zero) SetForegroundWindow(ednaHwnd);

                    // Restore the saved cursor position, or default to (20, 20) inside the EDNA client area.
                    if (ednaHwnd != IntPtr.Zero)
                    {
                        var target = _savedEdnaClientPos ?? new POINT { X = 20, Y = 20 };
                        if (ClientToScreen(ednaHwnd, ref target))
                            SetCursorPos(target.X, target.Y);
                    }

                    EdnaLogger.Detail("[WindowToggle] -> edna");
                }
            });
        }
    }
}
