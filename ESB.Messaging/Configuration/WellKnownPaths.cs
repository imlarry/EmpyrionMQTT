using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ESB.Messaging
{
    /// <summary>
    /// Paths and helpers for EDNA/ESB configuration files.
    /// Both ESB_Info.yaml and EDNA_Info.yaml are read from (and written to) the
    /// application's output directory, which is the ESB mod folder after deployment.
    /// </summary>
    public static class WellKnownPaths
    {
        // ESB_Info.yaml — placed next to the executable by deployment; read-only at runtime
        private static readonly string EsbInfoFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "ESB_Info.yaml");

        // EDNA_Info.yaml — read/write; lives alongside the executable in the mod folder
        public static readonly string EdnaInfoFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "EDNA_Info.yaml");

        // workspace_layout.xml — AvalonDock layout; written by WorkspaceWindow on close
        public static readonly string WorkspaceLayoutFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workspace_layout.xml");

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        /// <summary>Load ESB_Info.yaml from the app's output directory.</summary>
        public static Configuration.EsbInfo LoadEsbInfo()
            => LoadInfo<Configuration.EsbInfo>(EsbInfoFile);

        /// <summary>Deserialize a YAML file into T. Returns null if the file doesn't exist or parse fails.</summary>
        public static T LoadInfo<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                var yaml = File.ReadAllText(path);
                return Deserializer.Deserialize<T>(yaml);
            }
            catch { return null; }
        }

        /// <summary>Serialize obj to YAML and write to path (creates parent dirs as needed).</summary>
        public static void SaveInfo(string path, object obj)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serializer.Serialize(obj));
            }
            catch { }
        }
    }
}
