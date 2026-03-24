using System;

namespace ESB.Common
{
    public class MQTTConfig
    {
        public string WithTcpServer { get; set; }
        public int Port { get; set; } = 1883;
        public string Username { get; set; }
        public string Password { get; set; }
        public TimeSpan KeepAlivePeriod { get; set; }
        public string CAFilePath { get; set; }
    }
}