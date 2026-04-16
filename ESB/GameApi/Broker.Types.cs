using Eleon.Modding;

namespace ESB.GameApi
{
    public partial class Broker
    {
        private class ApiEvent
        {
            public CmdId  eventId;
            public ushort seqNr;
            public object data;

            public ApiEvent(CmdId eventId, ushort seqNr, object data)
            {
                this.eventId = eventId;
                this.seqNr   = seqNr;
                this.data    = data;
            }
        }
    }
}
