using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public class WinInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public string Name { get; private set; }
    public int? Left { get; }
    public int? Top { get; }
    public int? Right { get; }
    public int? Bottom { get; }
    public int? Width => Right.HasValue && Left.HasValue ? Right.Value - Left.Value : (int?)null;
    public int? Height => Bottom.HasValue && Top.HasValue ? Bottom.Value - Top.Value : (int?)null;

    public WinInfo()
    {
        Process currentProcess = Process.GetCurrentProcess();
        var hWnd = currentProcess.MainWindowHandle;
        if (hWnd != IntPtr.Zero)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            Name = sb.ToString();

            GetWindowRect(hWnd, out RECT windowRect);
            Left = windowRect.Left;
            Top = windowRect.Top;
            Right = windowRect.Right;
            Bottom = windowRect.Bottom;
        }
    }
}