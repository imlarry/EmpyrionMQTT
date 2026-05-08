using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace EDNAClient.Core
{
    /// <summary>
    /// Owns Win32 RegisterHotKey / UnregisterHotKey and dispatches WM_HOTKEY to
    /// skill callbacks.  Must be created and used on the WPF dispatcher thread.
    /// </summary>
    public sealed class HotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly HwndSource              _source;
        private readonly Dictionary<int, Action> _callbacks = new Dictionary<int, Action>();
        private int _nextId = 1;

        public HotkeyManager()
        {
            // Zero-size invisible window — exists only to receive WM_HOTKEY messages.
            var p = new HwndSourceParameters("EDNA_Hotkeys") { Width = 0, Height = 0, WindowStyle = 0 };
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        /// <summary>
        /// Registers a hotkey. Returns false if the combination is already held by
        /// another process; logs a warning but does not throw.
        /// </summary>
        public bool Register(HotkeyRequest request)
        {
            int id = _nextId++;
            if (!RegisterHotKey(_source.Handle, id, request.Modifiers, request.VirtualKey))
            {
                EdnaLogger.Warn($"[HotkeyManager] RegisterHotKey failed for id={id} " +
                               $"modifiers=0x{request.Modifiers:X} vk=0x{request.VirtualKey:X} " +
                               "(combo may be held by another app)");
                return false;
            }
            _callbacks[id] = request.Callback;
            return true;
        }

        /// <summary>Unregisters all currently registered hotkeys.</summary>
        public void UnregisterAll()
        {
            foreach (var id in _callbacks.Keys)
                UnregisterHotKey(_source.Handle, id);
            _callbacks.Clear();
            _nextId = 1;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _callbacks.TryGetValue(wParam.ToInt32(), out var cb))
            {
                cb();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }
}
