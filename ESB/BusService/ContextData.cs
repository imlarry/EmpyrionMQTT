using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eleon.Modding;
using ESB.GameApi;
using ESB.Configuration;
using ESB.Messaging;

namespace ESB
{
    public class ContextData : BaseContextData
    {
        // base context data includes the messenger

        public ContextData()
        {
        }

        // PROPERTIES
        public IModApi ModApi { get; set; }
        public EmpyrionModBase ModBase { get; set; }
        public ESBConfig ESBConfig { get; set; }
        public BusManager BusManager { get; set; }
        public GameManager GameManager { get; set; }
        public MainThreadRunner MainThreadRunner { get; } = new MainThreadRunner(); // should I constuct this here?

        public bool IsReady { get; set; }
        public Queue<Func<Task>> EventQueue { get; } = new Queue<Func<Task>>();
    }
}
