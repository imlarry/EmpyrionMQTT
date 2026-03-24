using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface IApplication
    {
        void Register();
        Task GetPathFor(string topic, string payload);
        Task GetAllPlayfields(string topic, string payload);
        Task GetPfServerInfos(string topic, string payload);
        Task GetPlayerEntityIds(string topic, string payload);
        Task GetPlayerDataFor(string topic, string payload);
        Task SendChatMessage(string topic, string payload);
        Task ShowDialogBox(string topic, string payload);
        Task GetStructure(string topic, string payload);
        Task GetStructures(string topic, string payload);
        Task GetBlockAndItemMapping(string topic, string payload);
        Task State(string topic, string payload);
        Task Mode(string topic, string payload);
        Task LocalPlayer(string topic, string payload);
        Task GameTicks(string topic, string payload);
    }
}
