using System.Threading.Tasks;

namespace ESB
{
    public interface ISubscriptionHandler
    {
        Task SubscribeAll();
    }
}