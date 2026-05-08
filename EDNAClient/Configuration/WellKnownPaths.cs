using System;
using System.Collections.Generic;
using System.IO;
using EDNAClient.Startup;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EDNAClient.Configuration
{
    public static class WellKnownPaths
    {
        // workspace_state.json -- UI state written by WorkspaceWindow; never overwritten by build
        public static readonly string WorkspaceStateFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workspace_state.json");

        // logs/ -- one log file per EDNA launch
        public static readonly string LogsDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "logs");

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        // Two-step lookup: deployed path first, then Steam registry fallback.
        public static string? LocateEsbInfoFile()
        {
            // Try 1: deployed alongside mod (ESBModPath\EDNA\EDNAClient.exe -> ..\ESB_Info.yaml)
            string relative = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ESB_Info.yaml"));
            if (File.Exists(relative)) return relative;

            // Try 2: Steam registry anchor
            return SteamLocator.GetEsbInfoPath();
        }

        public static EsbInfo? LoadEsbInfo() => LoadInfo<EsbInfo>(LocateEsbInfoFile());

        // Deserialize a YAML file into T. Returns null if the file doesn't exist or parse fails.
        public static T? LoadInfo<T>(string? path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                var yaml = File.ReadAllText(path);
                return Deserializer.Deserialize<T>(yaml);
            }
            catch (Exception ex)
            {
                EDNAClient.Core.EdnaLogger.Warn($"LoadInfo<{typeof(T).Name}> failed for '{path}': {ex.Message}");
                return null;
            }
        }

        // Serialize obj to YAML and write to path (creates parent dirs as needed).
        public static void SaveInfo(string path, object obj)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(path, Serializer.Serialize(obj));
            }
            catch (Exception ex)
            {
                EDNAClient.Core.EdnaLogger.Warn($"SaveInfo failed for '{path}': {ex.Message}");
            }
        }

        // Update only the EDNA block in ESB_Info.yaml, preserving all other fields.
        public static void SaveEdnaSettings(EdnaInfo edna)
        {
            string? path = LocateEsbInfoFile();
            if (path == null) return;
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var raw = File.Exists(path)
                    ? deserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(path))
                      ?? new Dictionary<object, object>()
                    : new Dictionary<object, object>();
                raw["EDNA"] = new Dictionary<object, object>
                {
                    { "EnabledSkillIds", new List<string>(edna.EnabledSkillIds) },
                    { "DetailEnabled",   edna.DetailEnabled }
                };
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .Build();
                File.WriteAllText(path, serializer.Serialize(raw));
            }
            catch (Exception ex)
            {
                EDNAClient.Core.EdnaLogger.Warn($"SaveEdnaSettings failed for '{path}': {ex.Message}");
            }
        }
    }
}
