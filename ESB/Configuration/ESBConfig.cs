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


        public class MQTTConfig
        {
            public string WithTcpServer { get; set; }
            public int Port { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public TimeSpan KeepAlivePeriod { get; set; }
            public string CAFilePath { get; set; }
        }

    }
}
