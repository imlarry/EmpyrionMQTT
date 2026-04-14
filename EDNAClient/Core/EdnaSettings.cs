using ESB.Messaging;
using ESB.Messaging.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace EDNAClient.Core
{
    public class EdnaSettings
    {
        private const string StartupKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupName = "EDNA";

        public HashSet<string> EnabledSkillIds { get; set; } = new() { "ThreatRadar" };

        public static EdnaSettings Load()
        {
            var info = WellKnownPaths.LoadInfo<EdnaInfo>(WellKnownPaths.EdnaInfoFile);
            return new EdnaSettings
            {
                EnabledSkillIds = info?.EnabledSkillIds is { Count: > 0 }
                    ? info.EnabledSkillIds
                    : new HashSet<string> { "ThreatRadar" }
            };
        }

        public void Save()
        {
            var info = WellKnownPaths.LoadInfo<EdnaInfo>(WellKnownPaths.EdnaInfoFile) ?? new EdnaInfo();
            info.EnabledSkillIds = EnabledSkillIds;
            WellKnownPaths.SaveInfo(WellKnownPaths.EdnaInfoFile, info);
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
