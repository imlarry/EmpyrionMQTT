using System;

namespace ESB.Common
{
    public class ESBConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string GIThub { get; set; } = string.Empty;
        public string ModTargets { get; set; } = string.Empty;
        public MQTTConfig MQTThost { get; set; } = new MQTTConfig();

    }
}
