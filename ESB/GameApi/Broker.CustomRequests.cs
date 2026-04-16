using Eleon.Modding;
using System;
using System.Threading.Tasks;

namespace ESB.GameApi
{
    public partial class Broker
    {
        // CmdId arithmetic used by the game for extended V1 requests (not in the CmdId enum).
        public async Task<GlobalStructureInfo> Request_GlobalStructure_Info(Id id)
            => await TaskTools.For(DefaultTimeout, SendRequestAsync<GlobalStructureInfo>(CmdId.Request_GlobalStructure_List + 100, id));

        public async Task<GlobalStructureInfo> Request_GlobalStructure_Info(Timeouts t, Id id)
        {
            try   { return await TaskTools.For(Span(t), SendRequestAsync<GlobalStructureInfo>(CmdId.Request_GlobalStructure_List + 100, id)); }
            catch (TaskCanceledException) { if ((int)t > 0) throw; return default; }
        }

        public async Task Request_SendChatMessage(Eleon.MessageData message)
        {
            try { await TaskTools.For(DefaultTimeout, SendRequestAsync<GlobalStructureInfo>(CmdId.Event_ChatMessage + 100, message)); }
            catch (TaskCanceledException) { }
        }

    }
}
