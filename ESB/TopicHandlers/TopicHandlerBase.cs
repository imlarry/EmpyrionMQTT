using ESB.Messaging;
using System;
using System.Threading.Tasks;

namespace ESB.TopicHandlers
{
    public abstract class TopicHandlerBase
    {
        protected readonly ContextData _ctx;

        protected TopicHandlerBase(ContextData ctx) { _ctx = ctx; }

        // Runs a handler on the main thread (Unity thread affinity for ModApi calls).
        // Synchronous bodies complete on the main thread; async-callback bodies make
        // their ModApi call on the main thread then await the engine callback. Unwrap
        // flattens the returned Task.
        protected Func<MessageEnvelope, Task<string>> OnMain(Func<MessageEnvelope, Task<string>> handler)
        {
            return env => _ctx.MainThreadRunner.RunOnMainThread<Task<string>>(() => handler(env)).Unwrap();
        }
    }
}
