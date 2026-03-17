using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IStructure
    {
        void Register();
        Task Info(string topic, string payload);
        Task Tanks(string topic, string payload);
        Task GetAllCustomDeviceNames(string topic, string payload);
        Task GetDevicePositions(string topic, string payload);
        Task SetFaction(string topic, string payload);
        Task AddTankContent(string topic, string payload);
        Task GetDockedVessels(string topic, string payload);
        Task GetPassengers(string topic, string payload);
        Task GetBlockSignals(string topic, string payload);
        Task GetControlPanelSignals(string topic, string payload);
        Task GetSignalState(string topic, string payload);
        Task GetSignalReceivers(string topic, string payload);
        Task GetSendSignalName(string topic, string payload);
        Task StructToGlobalPos(string topic, string payload);
        Task GlobalToStructPos(string topic, string payload);
    }
}
