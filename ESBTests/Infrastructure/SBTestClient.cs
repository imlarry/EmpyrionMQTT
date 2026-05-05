using ESB.Configuration;
using ESB.Helpers;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
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
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending
        = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();

    private SBTestClient(IMqttClient client, MqttFactory factory, string participantType, string clientId)
    {
        _client          = client;
        _factory         = factory;
        _participantType = participantType;
        _clientId        = clientId;
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

        var clientId = Guid.NewGuid().ToString("N").Substring(0, 4);
        var instance = new SBTestClient(client, factory, participantType, clientId);

        // Persistent response subscription -- receives replies to all RequestAsync calls.
        var subOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic($"ESB/{participantType}/{clientId}/+/res/+"))
            .Build();
        await client.SubscribeAsync(subOptions, CancellationToken.None);

        return instance;
    }

    /// <summary>
    /// Sends an ESB request and awaits the response via MQTT5 ResponseTopic + CorrelationData.
    /// Publishes to:  ESB/{participantType}/{clientId}/{scope}/req/{operation}
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
                .WithTopic($"ESB/{_participantType}/{_clientId}/{scope}/req/{operation}")
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

        var seg  = e.ApplicationMessage.PayloadSegment;
        var json = Encoding.UTF8.GetString(seg.Array!, seg.Offset, seg.Count);
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
