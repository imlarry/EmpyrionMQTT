using System.Threading.Tasks;

namespace ESB.Messaging
{
    // IPluginAction .. the required api for plugin methods
    public interface IPluginAction
    {
        Task Execute(string topic, string payload);
    }
}
