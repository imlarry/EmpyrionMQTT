using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface IPda
    {
        void Register();
        Task ShowMessage(string topic, string payload);
        Task ShowDialog(string topic, string payload);
        Task GiveReward(string topic, string payload);
        Task SpawnDropBox(string topic, string payload);
        Task SetMapMarker(string topic, string payload);
        Task GetPoiLocation(string topic, string payload);
        Task GetPoiEntityId(string topic, string payload);
        Task GetBlockLocation(string topic, string payload);
        Task GetBlockName(string topic, string payload);
        Task SpawnPrefabAtBlock(string topic, string payload);
        Task SpawnPrefabAtPosition(string topic, string payload);
        Task SpawnEntityAtPosition(string topic, string payload);
        Task CreateWaveAttack(string topic, string payload);
        Task CreateId(string topic, string payload);
        Task Activate(string topic, string payload);
    }
}
