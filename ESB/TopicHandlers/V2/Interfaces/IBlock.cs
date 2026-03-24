using System.Threading.Tasks;

namespace ESB.TopicHandlers.V2
{
    public interface IBlock
    {
        void Register();
        Task Get(string topic, string payload);
        Task Set(string topic, string payload);
        Task SetDamage(string topic, string payload);
        Task GetTextures(string topic, string payload);
        Task SetTextures(string topic, string payload);
        Task SetTextureForWholeBlock(string topic, string payload);
        Task GetColors(string topic, string payload);
        Task SetColors(string topic, string payload);
        Task SetColorForWholeBlock(string topic, string payload);
        Task GetSwitchState(string topic, string payload);
        Task SetSwitchState(string topic, string payload);
        Task SetLockCode(string topic, string payload);
    }
}
