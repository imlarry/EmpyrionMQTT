using Eleon.Modding;
using EmpyrionNetAPIAccess;
using System.Collections.Generic;

namespace ESB.Common
{
    public interface IContextData
    {
        IModApi ModApi { get; set; }
        EmpyrionModBase ModBase { get; set; }
        ESBConfig ESBConfig { get; set; }
        ESBManager ESBManager { get; set; }
        Dictionary<string, IPlayfield> LoadedPlayfield { get; set; }
        Dictionary<int, IEntity> LoadedEntity { get; set; }

        IPlayfield GetPlayfieldByKey(string Name);
        IEntity GetEntityByKey(int EntityId);
    }
}