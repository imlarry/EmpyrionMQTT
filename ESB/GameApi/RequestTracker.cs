using Eleon.Modding;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ESB.GameApi
{
    /// <summary>
    /// Correlates V1 async requests to their responses using sequence numbers.
    /// Game_Request fires a CmdId with a seqNr; the matching Event response carries
    /// the same seqNr back, which TryHandleEvent uses to resolve the waiting Task.
    /// </summary>
    public class RequestTracker
    {
        private static readonly object _seqLock = new object();
        private static int _nextSeq = new Random().Next(10000);

        private readonly ConcurrentDictionary<ushort, object> _pending =
            new ConcurrentDictionary<ushort, object>();

        internal Tuple<ushort, Task<T>> GetNewTaskCompletionSource<T>()
        {
            var tcs = new TaskCompletionSource<T>();
            Enqueue(tcs, out ushort seq);
            return Tuple.Create(seq, tcs.Task);
        }

        private void Enqueue<T>(TaskCompletionSource<T> tcs, out ushort seq)
        {
            if (_nextSeq == ushort.MaxValue)
                lock (_seqLock) { _nextSeq = 12340; }

            seq = (ushort)Interlocked.Increment(ref _nextSeq);
            while (!_pending.TryAdd(seq, tcs))
                seq = (ushort)Interlocked.Increment(ref _nextSeq);
        }

        internal bool TryHandleEvent(CmdId eventId, ushort seqNr, object data)
        {
            if (!_pending.TryRemove(seqNr, out object tcs))
                return false;

            if (eventId == CmdId.Event_Error && data is ErrorInfo eInfo)
            {
                var setEx = tcs.GetType().GetMethod("TrySetException", new[] { typeof(Exception) });
                setEx.Invoke(tcs, new object[] { new Exception(eInfo.errorType.ToString()) });
                return true;
            }

            try
            {
                if (tcs is TaskCompletionSource<bool> boolTcs)
                {
                    boolTcs.TrySetResult(true);
                }
                else
                {
                    var setResult = tcs.GetType().GetMethod("TrySetResult");
                    setResult.Invoke(tcs, new[] { data });
                }
                return true;
            }
            catch (Exception ex)
            {
                var setEx = tcs.GetType().GetMethod("TrySetException", new[] { typeof(Exception) });
                setEx.Invoke(tcs, new[] { ex });
                return false;
            }
        }
    }
}
