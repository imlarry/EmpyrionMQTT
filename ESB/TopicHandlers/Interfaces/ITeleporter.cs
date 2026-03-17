using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface ITeleporter
    {
        void Register();
        Task Get(string topic, string payload);
        Task Set(string topic, string payload);
    }
}
