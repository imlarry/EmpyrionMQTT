using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IGui
    {
        void Register();
        Task Edna_Selftest(string topic, string payload);
        Task ShowGameMessage(string topic, string payload);
        Task ShowDialog(string topic, string payload);
        Task IsWorldVisible(string topic, string payload);
    }
}