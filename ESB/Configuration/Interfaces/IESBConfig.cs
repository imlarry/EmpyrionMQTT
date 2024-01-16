using System;

namespace ESB.Common
{
    public interface IESBConfig
    {
        string Name { get; set; }
        string Description { get; set; }
        string Author { get; set; }
        string Version { get; set; }
        string GIThub { get; set; }
        string ModTargets { get; set; }
        MQTTConfig MQTThost { get; set; }
    }
}