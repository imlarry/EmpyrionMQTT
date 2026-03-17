using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IContainer
    {
        void Register();
        Task Get(string topic, string payload);
        Task Contains(string topic, string payload);
        Task GetTotalItems(string topic, string payload);
        Task AddItems(string topic, string payload);
        Task RemoveItems(string topic, string payload);
        Task Clear(string topic, string payload);
        Task SetContent(string topic, string payload);
    }
}
