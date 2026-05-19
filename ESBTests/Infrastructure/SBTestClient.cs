using ESB.Configuration;
using ESB.Helpers;
using ESB.Messaging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESBTests.Infrastructure;

/// <summary>
/// MQTT5-native test client for the ESB/ topic schema.
/// Connects as participantType "Test" with a random 4-char client ID.
/// Uses ResponseTopic + CorrelationData for request/reply.
///
/// Usage:
///   await using var mqtt = await SBTestClient.ConnectAsync();
///   var payload = await mqtt.RequestAsync("App", "GameTicks", "{}");
///   Assert.NotNull(payload["GameTicks"]);
/// </summary>
public sealed class SBTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory;
    private readonly string      _participantType;
    private readonly string      _clientId;
    private readonly string      _targetRcId;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending
        = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();

    private SBTestClient(IMqttClient client, MqttFactory factory, string participantType, string clientId, string targetRcId)
    {
        _client          = client;
        _factory         = factory;
        _participantType = participantType;
        _clientId        = clientId;
        _targetRcId      = targetRcId;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public static async Task<SBTestClient> ConnectAsync(string participantType = "Test")
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ESB_Info.yaml");
        var cfg = YamlFileReader.ReadYamlFile<ESBConfig>(configPath).MQTThost;

        var factory = new MqttFactory();
        var client  = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(cfg.WithTcpServer, cfg.Port)
            .WithProtocolVersion(MqttProtocolVersion.V500);

        if (!string.IsNullOrEmpty(cfg.Username))
            builder = builder.WithCredentials(cfg.Username, cfg.Password);

        await client.ConnectAsync(builder.Build(), CancellationToken.None);

        var clientId   = Guid.NewGuid().ToString("N").Substring(0, 8);
        var targetRcId = ReadTargetMachineId();
        var instance   = new SBTestClient(client, factory, participantType, clientId, targetRcId);

        // Persistent response subscription -- receives replies to all RequestAsync calls.
        var subOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic($"ESB/{participantType}/{clientId}/+/res/+"))
            .Build();
        await client.SubscribeAsync(subOptions, CancellationToken.None);

        return instance;
    }

    // Reads the persisted bus.token written by the in-process ESB Messenger and derives the
    // same MachineId. Tests run on the same machine as the ESB, so both processes resolve
    // identical paths and identical MachineIds. This is what the test client targets for
    // requests, since the ESB's only auto-subscription is its own MachineId rcId.
    private static string ReadTargetMachineId()
    {
        var tokenDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EmpyrionESB");
        var tokenPath = Path.Combine(tokenDir, "bus.token");
        if (!File.Exists(tokenPath))
            throw new InvalidOperationException("SBTestClient: bus.token not found at " + tokenPath + " -- start the ESB first so it creates the token");
        var token = File.ReadAllText(tokenPath).Trim();
        return RoutingContextId.Machine(token).Id;
    }

    /// <summary>
    /// Sends an ESB request and awaits the response via MQTT5 ResponseTopic + CorrelationData.
    /// Publishes to:  ESB/{participantType}/{esbMachineId}/{scope}/req/{operation}
    ///                (esbMachineId derived from the persisted bus.token so it matches the
    ///                ESB's own auto-subscription on its MachineId)
    /// ResponseTopic: ESB/{participantType}/{clientId}/{scope}/res/{operation}
    /// Errors are returned inside the res payload as {"Error": "..."}.
    /// </summary>
    public async Task<JObject> RequestAsync(
        string scope,
        string operation,
        string requestJson,
        int timeoutMs = 5000)
    {
        var shortId       = Guid.NewGuid().ToString("N").Substring(0, 8);
        var responseTopic = $"ESB/{_participantType}/{_clientId}/{scope}/res/{operation}";
        var tcs           = new TaskCompletionSource<JObject>();

        _pending[shortId] = tcs;
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"ESB/{_participantType}/{_targetRcId}/{scope}/req/{operation}")
                .WithPayload(requestJson)
                .WithResponseTopic(responseTopic)
                .WithCorrelationData(Encoding.ASCII.GetBytes(shortId))
                .Build();
            await _client.PublishAsync(message, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => tcs.TrySetCanceled());
            try
            {
                return await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"{scope}/{operation}: no response within {timeoutMs}ms");
            }
        }
        finally
        {
            _pending.TryRemove(shortId, out _);
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var corrData = e.ApplicationMessage.CorrelationData;
        if (corrData == null || corrData.Length == 0) return Task.CompletedTask;

        string shortId;
        try   { shortId = Encoding.ASCII.GetString(corrData); }
        catch { return Task.CompletedTask; }

        if (!_pending.TryGetValue(shortId, out var tcs)) return Task.CompletedTask;

        var seg = e.ApplicationMessage.PayloadSegment;
        var buf = new byte[seg.Count];
        Array.Copy(seg.Array, seg.Offset, buf, 0, seg.Count);

        string json;
        if (buf.Length >= 2 && buf[0] == 0x1F && buf[1] == 0x8B)
        {
            using (var ms = new MemoryStream(buf))
            using (var gz = new GZipStream(ms, CompressionMode.Decompress))
            using (var out_ = new MemoryStream())
            {
                gz.CopyTo(out_);
                json = Encoding.UTF8.GetString(out_.ToArray());
            }
        }
        else
        {
            json = Encoding.UTF8.GetString(buf);
        }
        try
        {
            var token = JToken.Parse(json);
            var jobj  = token as JObject ?? new JObject(new JProperty("items", token));
            tcs.TrySetResult(jobj);
        }
        catch (Exception ex) { tcs.TrySetException(ex); }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
    }
}
