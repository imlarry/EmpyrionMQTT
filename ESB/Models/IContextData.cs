using Eleon.Modding;
using EmpyrionNetAPIAccess;
using System.Collections.Generic;
using ESB.Database;

namespace ESB.Common
{
    public interface IContextData
    {
        IModApi ModApi { get; set; }
        EmpyrionModBase ModBase { get; set; }
        ESBConfig ESBConfig { get; set; }
        BusManager BusManager { get; set; }
        GameManager GameManager { get; set; }
        DbAccess DbAccess { get; set; }
        MainThreadRunner MainThreadRunner { get; }
        Dictionary<string, IPlayfield> LoadedPlayfield { get; set; }
        Dictionary<int, IEntity> LoadedEntity { get; set; }

        IPlayfield GetPlayfieldByKey(string Name);
        IEntity GetEntityByKey(int EntityId);
    }
}