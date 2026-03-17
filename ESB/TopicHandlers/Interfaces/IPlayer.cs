using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IPlayer
    {
        void Register();
        Task Teleport(string topic, string payload);
        Task SteamId(string topic, string payload);
        Task Stats(string topic, string payload);
    }
}
