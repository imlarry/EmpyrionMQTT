using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EDNAClient.Core;
using ESB.Messaging;

namespace EDNAClient.Helpers
{
    // Encapsulates the sequence-numbered R/X request-response pattern used by skills
    // that make synchronous-style ESB calls (FloorMapper, GalaxyMapSkill, etc.).
    //
    // Usage:
    //   _req = new MqttRequester();
    //   await _req.StartAsync(messenger, "V2.Player", "V2.Structure.ScanFloor");
    //   string body = await _req.RequestAsync("V2.Player", payload);
    //   _req.Stop();  // cancels in-flight requests
    internal sealed class MqttRequester
    {
        private const string EsbApp = "Client";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        private int _seqId;
        private readonly Dictionary<int, TaskCompletionSource<(bool Ok, string Body)>> _pending = new();
        private IMessenger? _messenger;

        public async Task StartAsync(IMessenger messenger, params string[] subjectIds)
        {
            _messenger = messenger;
            foreach (var s in subjectIds)
            {
                await messenger.SubscribeEventAsync($"+/R/{s}/+/+", OnAnyResponse);
                await messenger.SubscribeEventAsync($"+/X/{s}/+/+", OnAnyResponse);
            }
        }

        public void Stop()
        {
            lock (_pending)
            {
                foreach (var tcs in _pending.Values)
                    tcs.TrySetCanceled();
                _pending.Clear();
            }
            _messenger = null;
        }

        public async Task<string> RequestAsync(string subjectId, string payload)
        {
            if (_messenger == null) throw new InvalidOperationException("MqttRequester not started");

            int seq = Interlocked.Increment(ref _seqId);
            var tcs = new TaskCompletionSource<(bool Ok, string Body)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pending) _pending[seq] = tcs;
            try
            {
                EdnaLogger.Detail($"[MqttRequester] -> {subjectId} seq={seq}");
                await _messenger.PublishAsync($"{EsbApp}/Q/{subjectId}/*/{seq}", payload);

                var result = await tcs.Task.WaitAsync(Timeout);
                EdnaLogger.Detail($"[MqttRequester] <- {subjectId} seq={seq} ok={result.Ok}");
                if (!result.Ok)
                    throw new Exception($"ESB {subjectId}: {result.Body}");
                return result.Body;
            }
            finally
            {
                lock (_pending) _pending.Remove(seq);
            }
        }

        private Task OnAnyResponse(string topic, string payload)
        {
            var parts = topic.Split('/');
            if (parts.Length < 5) return Task.CompletedTask;
            if (!int.TryParse(parts[4], out int seq)) return Task.CompletedTask;

            bool isOk = parts[1] == "R";

            TaskCompletionSource<(bool, string)>? tcs;
            lock (_pending)
            {
                if (!_pending.TryGetValue(seq, out tcs)) return Task.CompletedTask;
                _pending.Remove(seq);
            }
            tcs.TrySetResult((isOk, payload ?? ""));
            return Task.CompletedTask;
        }
    }
}
