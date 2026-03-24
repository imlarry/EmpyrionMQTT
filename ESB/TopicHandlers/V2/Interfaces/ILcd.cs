using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface ILcd
    {
        void Register();
        Task Get(string topic, string payload);
        Task SetText(string topic, string payload);
        Task SetTextColor(string topic, string payload);
        Task SetBackgroundColor(string topic, string payload);
        Task SetFontSize(string topic, string payload);
    }
}
