using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace EDNAClient.Core
{
    public static class GameWindowLocator
    {
        private const string GameWindowTitle = "Empyrion - Galactic Survival";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        /// <summary>
        /// Returns the raw Win32 handle for the game window, or IntPtr.Zero if not found.
        /// </summary>
        public static IntPtr GetWindowHandle() => FindWindow(null, GameWindowTitle);

        /// <summary>
        /// Returns the game client area as a WPF logical-pixel Rect,
        /// or null if the game window is not found.
        /// </summary>
        public static Rect? GetClientRect()
        {
            var hwnd = FindWindow(null, GameWindowTitle);
            if (hwnd == IntPtr.Zero)
                return null;

            if (!GetClientRect(hwnd, out RECT cr))
                return null;

            var origin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref origin))
                return null;

            double scale = GetDpiScale();
            return new Rect(
                origin.X / scale,
                origin.Y / scale,
                (cr.Right - cr.Left) / scale,
                (cr.Bottom - cr.Top) / scale);
        }

        private static double GetDpiScale()
        {
            int physicalWidth = GetSystemMetrics(SM_CXSCREEN);
            double logicalWidth = SystemParameters.PrimaryScreenWidth;
            return physicalWidth > 0 && logicalWidth > 0
                ? physicalWidth / logicalWidth
                : 1.0;
        }
    }
}
