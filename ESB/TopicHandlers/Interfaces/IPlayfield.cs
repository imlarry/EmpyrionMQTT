using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IPlayfield
    {
        Task Subscribe();
        Task SpawnEntity(string topic, string payload);
        Task SpawnPrefab(string topic, string payload);
        Task RemoveEntity(string topic, string payload);
        Task IsStructureDeviceLocked(string topic, string payload);
        Task MoveEntity(string topic, string payload);
    }
}