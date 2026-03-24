using System.Threading.Tasks;

namespace ESB.TopicHandlers.V1
{
    public interface IPlayer
    {
        void Register();
        Task GetInventory(string topic, string payload);
    }
}
