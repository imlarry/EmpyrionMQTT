using System.Collections.Generic;
using System.Threading.Tasks;
using ESB.Messaging;

namespace ESB
{
    // Tracks retained topics this participant owns so they can be null-posted on graceful
    // disconnect or on Lobby<->Game switch. Lives outside ESB.Messaging so the messaging core
    // stays generic; the bus exposes a SetBeforeDisconnect hook that this class plugs into.
    public class DisconnectCleanup
    {
        private struct Entry
        {
            public string Rc;
            public string Scope;
            public string Operation;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly object _lock = new object();

        public void Register(string rcId, string scope, string operation)
        {
            if (string.IsNullOrEmpty(rcId)) return;
            lock (_lock)
            {
                _entries.Add(new Entry { Rc = rcId, Scope = scope, Operation = operation });
            }
        }

        public void Unregister(string rcId, string scope, string operation)
        {
            lock (_lock)
            {
                _entries.RemoveAll(e => e.Rc == rcId && e.Scope == scope && e.Operation == operation);
            }
        }

        // Null-posts every registered topic and empties the registry.
        public async Task ClearAllAsync(IMessenger messenger)
        {
            List<Entry> snapshot;
            lock (_lock)
            {
                snapshot = new List<Entry>(_entries);
                _entries.Clear();
            }
            foreach (var e in snapshot)
            {
                try { await messenger.PublishRetainedAsync(e.Rc, e.Scope, MessageType.Evt, e.Operation, null, 0u, false); }
                catch { /* best-effort cleanup */ }
            }
        }

        // Null-posts every topic registered under rcId and removes them from the registry.
        public async Task ClearScopeAsync(IMessenger messenger, string rcId)
        {
            if (string.IsNullOrEmpty(rcId)) return;
            List<Entry> match;
            lock (_lock)
            {
                match = new List<Entry>();
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (_entries[i].Rc == rcId)
                    {
                        match.Add(_entries[i]);
                        _entries.RemoveAt(i);
                    }
                }
            }
            foreach (var e in match)
            {
                try { await messenger.PublishRetainedAsync(e.Rc, e.Scope, MessageType.Evt, e.Operation, null, 0u, false); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
