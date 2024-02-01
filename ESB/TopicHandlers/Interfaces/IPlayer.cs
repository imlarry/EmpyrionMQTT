using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IPlayer
    {
        Task Subscribe();
        Task Teleport(string topic, string payload);
        Task SteamId(string topic, string payload);
    }
}
