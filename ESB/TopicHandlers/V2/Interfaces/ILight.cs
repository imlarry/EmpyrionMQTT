using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface ILight
    {
        void Register();
        Task Get(string topic, string payload);
        Task SetColor(string topic, string payload);
        Task SetIntensity(string topic, string payload);
        Task SetRange(string topic, string payload);
        Task SetLightType(string topic, string payload);
        Task SetBlinkData(string topic, string payload);
        Task SetSpotAngle(string topic, string payload);
    }
}
