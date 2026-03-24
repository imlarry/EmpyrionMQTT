namespace ESB.Messaging.Configuration
{
    public class MqttConnectionSettings
    {
        public string  WithTcpServer { get; set; } = "localhost";
        public int     Port          { get; set; } = 1883;
        public string Username      { get; set; }
        public string Password      { get; set; }
        public string CAFilePath    { get; set; }
    }
}
