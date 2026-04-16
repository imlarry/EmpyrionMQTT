using Eleon.Modding;

namespace ESB.GameApi
{
    public static class BrokerExtensions
    {
        /// <summary>Creates an Eleon Id from an integer.</summary>
        public static Id ToId(this int anId) => new Id(anId);

        /// <summary>Creates an IdMsgPrio for sending a message to a specific player.</summary>
        public static IdMsgPrio ToIdMsgPrio(this string msg, int id,
            MessagePriorityType prio = MessagePriorityType.Alarm, float duration = 3.0F)
            => new IdMsgPrio { id = id, msg = msg, prio = (byte)prio, time = duration };

        /// <summary>Converts a string to a PString.</summary>
        public static PString ToPString(this string msg) => new PString(msg);

        /// <summary>Formats a PVector3 as "x,y,z" with one decimal place.</summary>
        public static string String(this PVector3 v) => $"{v.x:F1},{v.y:F1},{v.z:F1}";
    }
}
