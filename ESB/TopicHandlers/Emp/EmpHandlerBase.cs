using ESB.Messaging;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESB.TopicHandlers.Emp
{
    // Shared helpers for emp/ schema handlers.
    internal static class EmpHandlerBase
    {
        // Reply with a success payload.
        internal static Task ReplyAsync(IMessenger messenger, EmpMessageContext ctx, string payload)
            => messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, payload);

        // Reply with an error payload on the err topic derived from the request topic.
        // Replaces the dir segment with "Err": EMP/{type}/{connId}/{scope}/Err/{op}
        internal static async Task ReplyErrorAsync(IMessenger messenger, EmpMessageContext ctx, string errorJson)
        {
            // Send error to caller via ResponseTopic (they are subscribed there)
            await messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, errorJson);

            // Also publish to the err topic for bus observers
            var pt = ctx.ParsedTopic;
            string errTopic = pt.DeviceName != null
                ? $"EMP/{pt.ParticipantType}/{pt.ConnectionId}/Structure/Device/{pt.DeviceName}/Err/{pt.Operation}"
                : $"EMP/{pt.ParticipantType}/{pt.ConnectionId}/{pt.Scope}/Err/{pt.Operation}";
            await messenger.SendAsync(errTopic, errorJson);
        }

        internal static byte[] ToBytes(string s) => Encoding.UTF8.GetBytes(s);
    }
}
