namespace EDNAClient.Configuration
{
    public class EsbInfo
    {
        public MqttConnectionSettings? MQTThost { get; set; }
        public EdnaInfo EDNA { get; set; } = new EdnaInfo();
    }
}
