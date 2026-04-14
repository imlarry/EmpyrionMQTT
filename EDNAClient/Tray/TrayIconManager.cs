using EDNAClient.Core;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace EDNAClient.Tray
{
    public class TrayIconManager : IDisposable
    {
        // ── DWM ────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUNDSMALL              = 3;

        // ── Colors (dark scheme) ───────────────────────────────────────────
        private static readonly Color DarkBg      = Color.FromArgb(0x20, 0x20, 0x20);
        private static readonly Color DarkHover   = Color.FromArgb(0x3A, 0x3A, 0x3A);
        private static readonly Color DarkBorder  = Color.FromArgb(0x45, 0x45, 0x45);
        private static readonly Color DarkFg      = Color.FromArgb(0xF3, 0xF3, 0xF3);
        private static readonly Color DarkDisable = Color.FromArgb(0x86, 0x86, 0x86);

        // ── Tray icon colors (matching HUD indicator) ──────────────────────
        private static readonly Color TrayOffline = Color.FromArgb(0x66, 0x66, 0x66);
        private static readonly Color TrayHealthy = Color.FromArgb(0x00, 0xFF, 0x88);
        private static readonly Color TrayWarning = Color.FromArgb(0xFF, 0xA5, 0x00);
        private static readonly Color TrayError   = Color.FromArgb(0xFF, 0x44, 0x44);

        // ── State ──────────────────────────────────────────────────────────
        private readonly NotifyIcon        _notifyIcon;
        private readonly ContextMenuStrip  _menu;
        private readonly ToolStripMenuItem _statusItem;
        private readonly Action            _openSettings;
        private readonly bool              _dark;

        public TrayIconManager(Action openSettings)
        {
            _openSettings = openSettings;
            _dark         = IsSystemDarkMode();

            _statusItem = new ToolStripMenuItem("EDNA \u2014 Waiting for game") { Enabled = false };

            var settingsItem = new ToolStripMenuItem("Settings\u2026");
            settingsItem.Click += (_, _) => _openSettings();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) =>
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());

            _menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false };
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(settingsItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            if (_dark) ApplyDarkTheme(_menu);

            // Apply DWM rounded corners + dark non-client area when the popup is first shown
            _menu.Opening += (_, _) =>
            {
                if (_menu.Handle != IntPtr.Zero)
                {
                    int dark = _dark ? 1 : 0;
                    DwmSetWindowAttribute(_menu.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                    int corner = DWMWCP_ROUNDSMALL;
                    DwmSetWindowAttribute(_menu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                }
            };

            _notifyIcon = new NotifyIcon
            {
                Text             = "EDNA \u2014 Waiting for game",
                Icon             = MakeIcon(TrayOffline),
                ContextMenuStrip = _menu,
                Visible          = true
            };
            _notifyIcon.DoubleClick += (_, _) => _openSettings();
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void UpdateState(IndicatorState state, bool gameRunning)
        {
            var (color, label) = state switch
            {
                IndicatorState.Healthy => (TrayHealthy, "EDNA \u2014 Active"),
                IndicatorState.Warning => (TrayWarning, "EDNA \u2014 Warning"),
                IndicatorState.Error   => (TrayError,   "EDNA \u2014 Error"),
                _                      => (TrayOffline,  "EDNA \u2014 Waiting for game"),
            };

            _notifyIcon.Icon = MakeIcon(color);
            _notifyIcon.Text = label;    // 63-char WinForms limit
            SetItemText(_statusItem, label);
        }

        public void ShowBalloon(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText  = message;
            _notifyIcon.BalloonTipIcon  = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(4000);
        }

        public void OnGameStarted() { }

        public void OnGameExited() { }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
        }

        // ── Private helpers ────────────────────────────────────────────────

        // SetItemText must marshal to UI thread because NotifyIcon callbacks fire on background threads
        private static void SetItemText(ToolStripItem item, string text)
        {
            if (item.Owner?.InvokeRequired == true)
                item.Owner.Invoke(() => item.Text = text);
            else
                item.Text = text;
        }

        private void ApplyDarkTheme(ContextMenuStrip menu)
        {
            menu.Renderer  = new DarkMenuRenderer();
            menu.BackColor = DarkBg;
            menu.ForeColor = DarkFg;

            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = DarkBg;
                item.ForeColor = item.Enabled ? DarkFg : DarkDisable;

                if (item is ToolStripSeparator sep)
                    sep.Paint += PaintDarkSeparator;
            }
        }

        private static void PaintDarkSeparator(object? sender, PaintEventArgs e)
        {
            if (sender is not ToolStripSeparator sep) return;
            e.Graphics.Clear(DarkBg);
            int mid = sep.Height / 2;
            using var pen = new Pen(DarkBorder);
            e.Graphics.DrawLine(pen, 8, mid, sep.Width - 8, mid);
        }

        private static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }

        private static Icon MakeIcon(Color color)
        {
            using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, 3, 3, 26, 26);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ── Dark renderer ──────────────────────────────────────────────────

        private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? DarkFg : DarkDisable;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using var brush = new SolidBrush(e.Item.Selected && e.Item.Enabled ? DarkHover : DarkBg);
                e.Graphics.FillRectangle(brush, rect);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(DarkBg);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(DarkBorder);
                e.Graphics.DrawRectangle(pen, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
            }
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder                    => DarkBorder;
            public override Color MenuItemBorder                => Color.Transparent;
            public override Color MenuItemSelected              => DarkHover;
            public override Color MenuItemSelectedGradientBegin => DarkHover;
            public override Color MenuItemSelectedGradientEnd   => DarkHover;
            public override Color MenuItemPressedGradientBegin  => DarkHover;
            public override Color MenuItemPressedGradientEnd    => DarkHover;
            public override Color ToolStripDropDownBackground   => DarkBg;
        }
    }
}
