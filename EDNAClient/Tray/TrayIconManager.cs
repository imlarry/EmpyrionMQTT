using EDNAClient.Core;
using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;

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

        // ── Colors ─────────────────────────────────────────────────────────
        private static readonly Color DarkBg     = Color.FromArgb(0x20, 0x20, 0x20);
        private static readonly Color DarkHover  = Color.FromArgb(0x3A, 0x3A, 0x3A);
        private static readonly Color DarkBorder = Color.FromArgb(0x45, 0x45, 0x45);
        private static readonly Color DarkFg     = Color.FromArgb(0xF3, 0xF3, 0xF3);
        private static readonly Color TrayGreen  = Color.FromArgb(0x00, 0xFF, 0x88);
        private static readonly Color TrayRed    = Color.FromArgb(0xFF, 0x44, 0x44);

        // ── State ──────────────────────────────────────────────────────────
        private readonly NotifyIcon _notifyIcon;
        private readonly bool       _dark;
        private Timer?              _blinkTimer;
        private Icon?               _blinkIconOn;
        private Icon?               _blinkIconOff;
        private volatile bool       _blinkOn;

        public TrayIconManager()
        {
            _dark = IsSystemDarkMode();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) =>
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());

            var menu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = false };
            menu.Items.Add(exitItem);
            if (_dark) ApplyDarkTheme(menu);

            menu.Opening += (_, _) =>
            {
                if (menu.Handle != IntPtr.Zero)
                {
                    int dark = _dark ? 1 : 0;
                    DwmSetWindowAttribute(menu.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                    int corner = DWMWCP_ROUNDSMALL;
                    DwmSetWindowAttribute(menu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                }
            };

            _notifyIcon = new NotifyIcon
            {
                Text             = "EDNA - Connecting...",
                Icon             = MakeIcon(TrayRed),
                ContextMenuStrip = menu,
                Visible          = true
            };

            StartBlink(TrayRed, "EDNA - Connecting...");
        }

        // ── Public API ─────────────────────────────────────────────────────

        public void SetMqttDown()  => StartBlink(TrayRed,   "EDNA - No MQTT connection");
        public void SetConnected() => StartBlink(TrayGreen, "EDNA - Connected");
        public void SetInGame()    => SetSolid  (TrayGreen, "EDNA - In Game");

        public void ShowBalloon(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText  = message;
            _notifyIcon.BalloonTipIcon  = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(4000);
        }

        public void UpdateLocation(string solarSystem, string playfield)
        {
            string text = $"EDNA - {solarSystem} / {playfield}";
            Dispatch(() => _notifyIcon.Text = Tip(text));
        }

        public void ClearLocation() { }

        public void Dispose()
        {
            StopBlink();
            _blinkIconOn?.Dispose();
            _blinkIconOff?.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        // ── Private ────────────────────────────────────────────────────────

        private void StartBlink(Color color, string tooltip)
        {
            StopBlink();
            _blinkIconOn?.Dispose();
            _blinkIconOff?.Dispose();
            _blinkIconOn  = MakeIcon(color);
            _blinkIconOff = MakeIcon(DarkBg);
            _blinkOn = true;
            Dispatch(() => { _notifyIcon.Icon = _blinkIconOn; _notifyIcon.Text = Tip(tooltip); });
            _blinkTimer = new Timer(_ => Tick(), null, 800, 800);
        }

        private void SetSolid(Color color, string tooltip)
        {
            StopBlink();
            _blinkIconOn?.Dispose();
            _blinkIconOff?.Dispose();
            _blinkIconOn = _blinkIconOff = null;
            var icon = MakeIcon(color);
            Dispatch(() => { _notifyIcon.Icon = icon; _notifyIcon.Text = Tip(tooltip); });
        }

        private void StopBlink()
        {
            _blinkTimer?.Dispose();
            _blinkTimer = null;
        }

        private void Tick()
        {
            _blinkOn = !_blinkOn;
            var icon = _blinkOn ? _blinkIconOn : _blinkIconOff;
            if (icon != null)
                Dispatch(() => _notifyIcon.Icon = icon);
        }

        private static void Dispatch(Action a) =>
            Application.Current?.Dispatcher.Invoke(a);

        private static string Tip(string s) => s.Length > 63 ? s[..63] : s;

        private void ApplyDarkTheme(ContextMenuStrip menu)
        {
            menu.Renderer  = new DarkMenuRenderer();
            menu.BackColor = DarkBg;
            menu.ForeColor = DarkFg;
            foreach (ToolStripItem item in menu.Items)
            {
                item.BackColor = DarkBg;
                item.ForeColor = DarkFg;
            }
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
                e.TextColor = DarkFg;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rect = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
                using var brush = new SolidBrush(e.Item.Selected ? DarkHover : DarkBg);
                e.Graphics.FillRectangle(brush, rect);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
                e.Graphics.Clear(DarkBg);

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
