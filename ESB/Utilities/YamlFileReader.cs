using System;
using System.IO;
using YamlDotNet.Serialization;

namespace ESB.Common
{
    public static class YamlFileReader
    {
        public static T ReadYamlFile<T>(string filename) where T : class
        {
            try
            {
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                using (var reader = new StringReader(File.ReadAllText(filename)))
                {
                    var esbConfig = deserializer.Deserialize<T>(reader);
                    return esbConfig;
                }
            }
            catch (Exception ex)
            {
                // Consider using a logging framework here to log errors
                throw new Exception($"Error parsing YAML file: {ex.Message}", ex);
            }
        }
    }
}
