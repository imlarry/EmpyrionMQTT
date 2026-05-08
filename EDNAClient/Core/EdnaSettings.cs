using ESB.Messaging;
using EDNAClient.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace EDNAClient.Core
{
    public class EdnaSettings
    {
        private const string StartupKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupName = "EDNA";

        public HashSet<string> EnabledSkillIds { get; set; } = new() { "ThreatRadar", "FloorMap", "ScriptEditor", "GalaxyMap" };

        public static EdnaSettings Load()
        {
            var info = WellKnownPaths.LoadEsbInfo()?.EDNA;
            EdnaLogger.DetailEnabled = info?.DetailEnabled ?? false;
            return new EdnaSettings
            {
                EnabledSkillIds = info?.EnabledSkillIds is { Count: > 0 }
                    ? info.EnabledSkillIds
                    : new HashSet<string> { "ThreatRadar", "FloorMap", "ScriptEditor", "GalaxyMap" },
            };
        }

        public void Save()
        {
            var info = WellKnownPaths.LoadEsbInfo()?.EDNA ?? new EdnaInfo();
            info.EnabledSkillIds = EnabledSkillIds;
            WellKnownPaths.SaveEdnaSettings(info);
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
    }
}
