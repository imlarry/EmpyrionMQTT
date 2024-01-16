using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IApplication
    {
        Task Subscribe();
        Task Teleport(string topic, string payload);
        Task DumpMemory(string topic, string payload);
        Task WindowInfo(string topic, string payload);
        Task TraceEntity(string topic, string payload);
        Task ShowEntity(string topic, string payload);
        Task Player_GetInventory(string topic, string payload);
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