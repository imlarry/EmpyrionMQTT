using Eleon.Modding;
using EmpyrionNetAPIAccess;
using ESB.BusService;
using ESB.Configuration;
using System.Collections.Generic;

namespace ESB.Models
{
    public interface IContextData
    {
        IModApi ModApi { get; set; }
        EmpyrionModBase ModBase { get; set; }
        IESBConfig ESBConfig { get; set; }
        IBusManager BusManager { get; set; }
        IGameManager GameManager { get; set; }
        MainThreadRunner MainThreadRunner { get; }
        Dictionary<string, IPlayfield> LoadedPlayfield { get; set; }
        Dictionary<int, IEntity> LoadedEntity { get; set; }

        IPlayfield GetPlayfieldByKey(string Name);
        IEntity GetEntityByKey(int EntityId);
    }
}