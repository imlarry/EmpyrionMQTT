using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface ITeleporter
    {
        void Register();
        Task Get(string topic, string payload);
        Task Set(string topic, string payload);
    }
}
