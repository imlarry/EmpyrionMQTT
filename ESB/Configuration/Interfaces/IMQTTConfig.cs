using System;

namespace ESB.Common
{
    public interface IMQTTConfig
    {
        string WithTcpServer { get; set; }
        int Port { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        TimeSpan KeepAlivePeriod { get; set; }
        string CAFilePath { get; set; }
    }
}