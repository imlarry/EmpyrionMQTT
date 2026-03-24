using System;
using System.Runtime.InteropServices;

namespace EDNAClient.Core
{
    /// <summary>
    /// Hooks Win32 window events on the Empyrion game process so EDNA can re-snap
    /// whenever the game window is moved or resized — without polling.
    /// Must be created on a thread with a message pump (WPF dispatcher thread).
    /// </summary>
    public sealed class GameWindowEventHook : IDisposable
    {
        private const uint EVENT_SYSTEM_MOVESIZEEND    = 0x000B; // user finishes dragging
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B; // programmatic move/resize
        private const uint WINEVENT_OUTOFCONTEXT       = 0x0000;
        private const int  OBJID_WINDOW                = 0;
        private const int  CHILDID_SELF                = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private readonly WinEventProc _callback; // field keeps delegate alive so GC won't collect it
        private readonly IntPtr _gameHwnd;
        private readonly Action _onChanged;
        private IntPtr _hookMoveSize;
        private IntPtr _hookLocation;

        public GameWindowEventHook(IntPtr gameHwnd, Action onChanged)
        {
            _gameHwnd  = gameHwnd;
            _onChanged = onChanged;
            _callback  = OnWinEvent;

            GetWindowThreadProcessId(gameHwnd, out uint pid);
            _hookMoveSize = SetWinEventHook(
                EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero, _callback, pid, 0, WINEVENT_OUTOFCONTEXT);
            _hookLocation = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, _callback, pid, 0, WINEVENT_OUTOFCONTEXT);
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd != _gameHwnd) return;
            if (eventType == EVENT_OBJECT_LOCATIONCHANGE &&
                (idObject != OBJID_WINDOW || idChild != CHILDID_SELF)) return;

            _onChanged();
        }

        public void Dispose()
        {
            if (_hookMoveSize != IntPtr.Zero) { UnhookWinEvent(_hookMoveSize); _hookMoveSize = IntPtr.Zero; }
            if (_hookLocation != IntPtr.Zero) { UnhookWinEvent(_hookLocation); _hookLocation = IntPtr.Zero; }
        }
    }
}
