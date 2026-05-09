using System;

namespace ESB.Messaging
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BusRouteAttribute : Attribute
    {
        public string Scope { get; }
        public string Operation { get; }

        public BusRouteAttribute(string scope, string operation)
        {
            Scope = scope;
            Operation = operation;
        }
    }
}
