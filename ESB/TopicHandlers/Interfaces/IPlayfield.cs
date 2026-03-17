using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public interface IPlayfield
    {
        void Register();
        Task Info(string topic, string payload);
        Task SpawnEntity(string topic, string payload);
        Task SpawnPrefab(string topic, string payload);
        Task RemoveEntity(string topic, string payload);
        Task IsStructureDeviceLocked(string topic, string payload);
        Task MoveEntity(string topic, string payload);
    }
}