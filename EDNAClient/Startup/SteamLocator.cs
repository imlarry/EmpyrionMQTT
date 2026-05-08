using Microsoft.Win32;
using System.IO;

namespace EDNAClient.Startup
{
    public static class SteamLocator
    {
        public static string? GetSteamPath()
        {
            string? path = (string?)Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            return path?.Replace('/', '\\');
        }

        public static string? GetEmpyrionPath()
        {
            string? steam = GetSteamPath();
            if (steam == null) return null;
            string path = Path.Combine(steam, "steamapps", "common",
                "Empyrion - Galactic Survival");
            return Directory.Exists(path) ? path : null;
        }

        public static string? GetEsbInfoPath()
        {
            string? emp = GetEmpyrionPath();
            if (emp == null) return null;
            string path = Path.Combine(emp, "Content", "Mods", "ESB", "ESB_Info.yaml");
            return File.Exists(path) ? path : null;
        }
    }
}
