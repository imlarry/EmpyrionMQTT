using System;

namespace ESB.Messaging
{
    public class BusRequestException : Exception
    {
        public string BusError { get; }

        public BusRequestException(string busError)
            : base("Bus request failed: " + busError)
        {
            BusError = busError;
        }
    }
}
