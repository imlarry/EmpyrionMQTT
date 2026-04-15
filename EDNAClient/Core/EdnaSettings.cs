using ESB.Messaging;
using EDNAClient.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace EDNAClient.Core
{
    public class EdnaSettings
    {
        private const string StartupKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupName = "EDNA";

        public HashSet<string> EnabledSkillIds { get; set; } = new() { "ThreatRadar" };

        // Workspace window position/size -- null means use default on first launch
        public Rect? WorkspaceBounds { get; set; }

        // AvalonDock layout XML -- null means use default XAML layout
        public string? WorkspaceLayout { get; set; }

        public static EdnaSettings Load()
        {
            var info = WellKnownPaths.LoadInfo<EdnaInfo>(WellKnownPaths.EdnaInfoFile);
            var layout = LoadLayoutFile();
            return new EdnaSettings
            {
                EnabledSkillIds = info?.EnabledSkillIds is { Count: > 0 }
                    ? info.EnabledSkillIds
                    : new HashSet<string> { "ThreatRadar" },
                WorkspaceBounds = ParseBounds(info?.WorkspaceBounds),
                WorkspaceLayout = layout,
            };
        }

        public void Save()
        {
            var info = WellKnownPaths.LoadInfo<EdnaInfo>(WellKnownPaths.EdnaInfoFile) ?? new EdnaInfo();
            info.EnabledSkillIds = EnabledSkillIds;
            info.WorkspaceBounds = FormatBounds(WorkspaceBounds);
            WellKnownPaths.SaveInfo(WellKnownPaths.EdnaInfoFile, info);
            SaveLayoutFile(WorkspaceLayout);
        }

        public bool GetRunAtStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey, false);
            return key?.GetValue(StartupName) != null;
        }

        public void SetRunAtStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKey, true)!;
            if (enable)
                key.SetValue(StartupName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(StartupName, throwOnMissingValue: false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Rect? ParseBounds(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var parts = s.Split(',');
            if (parts.Length != 4) return null;
            if (double.TryParse(parts[0], out double l) &&
                double.TryParse(parts[1], out double t) &&
                double.TryParse(parts[2], out double w) &&
                double.TryParse(parts[3], out double h) &&
                w > 0 && h > 0)
                return new Rect(l, t, w, h);
            return null;
        }

        private static string? FormatBounds(Rect? r) =>
            r == null ? null
                : $"{r.Value.Left},{r.Value.Top},{r.Value.Width},{r.Value.Height}";

        private static string? LoadLayoutFile()
        {
            try
            {
                var path = WellKnownPaths.WorkspaceLayoutFile;
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch { return null; }
        }

        private static void SaveLayoutFile(string? xml)
        {
            try
            {
                var path = WellKnownPaths.WorkspaceLayoutFile;
                if (xml == null)
                    File.Delete(path);
                else
                    File.WriteAllText(path, xml);
            }
            catch { }
        }
    }
}
