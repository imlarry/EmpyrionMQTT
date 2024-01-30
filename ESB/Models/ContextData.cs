using Eleon.Modding;
using EmpyrionNetAPIAccess;
using ESB.Database;
using ESB.Messaging;
using System.Collections.Generic;

namespace ESB.Common
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
        public DbAccess DbAccess { get; set; }
        public MainThreadRunner MainThreadRunner { get; } = new MainThreadRunner();

        // cache prealloc to expected max + safety to avoid GC (TODO: dial in via dynamic alloc and watching actual max)
        private const int PlayfieldListEntries = 5;
        private const int EntityListEntries = 100;
        public Dictionary<string, IPlayfield> LoadedPlayfield { get; set; } = new Dictionary<string, IPlayfield>(PlayfieldListEntries);
        public Dictionary<int, IEntity> LoadedEntity { get; set; } = new Dictionary<int, IEntity>(EntityListEntries);

        // METHODS
        public IPlayfield GetPlayfieldByKey(string Name)
        {
            return LoadedPlayfield.TryGetValue(Name, out var playfield) ? playfield : null;
        }
        public IEntity GetEntityByKey(int EntityId)
        {
            return LoadedEntity.TryGetValue(EntityId, out var entity) ? entity : null;
        }
    }
}
