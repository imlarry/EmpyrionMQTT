using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface IGui
    {
        void Register();
        Task ShowGameMessage(string topic, string payload);
        Task ShowDialog(string topic, string payload);
        Task IsWorldVisible(string topic, string payload);
    }
}
