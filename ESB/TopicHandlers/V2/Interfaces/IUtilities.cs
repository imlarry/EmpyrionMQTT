using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface IUtilities
    {
        void Register();
        Task TestSelf(string topic, string payload);
        Task Teleport(string topic, string payload);
        Task DumpMemory(string topic, string payload);
        Task WindowInfo(string topic, string payload);
        Task TraceEntity(string topic, string payload);
        Task ShowEntity(string topic, string payload);
    }
}
