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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESBTests.Infrastructure;

/// <summary>
/// MQTT5-native test client for the emp/ topic schema.
/// Uses ResponseTopic + CorrelationData for request/reply rather than topic substitution.
///
/// Usage:
///   await using var mqtt = await EmpTestClient.ConnectAsync();
///   string connId = await mqtt.FindConnectionAsync("client");
///   var payload = await mqtt.RequestAsync(connId, "client", "app", "get/GameTicks", "{}");
///   Assert.NotNull(payload["GameTicks"]);
/// </summary>
public sealed class EmpTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory;
    // Local participant type and connectionId for the response topic.
    private const string TestParticipantType = "agent";
    private readonly string _testConnId;

    private EmpTestClient(IMqttClient client, MqttFactory factory)
    {
        _client   = client;
        _factory  = factory;
        _testConnId = Guid.NewGuid().ToString("N").Substring(0, 4);
    }

    public static async Task<EmpTestClient> ConnectAsync()
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
        return new EmpTestClient(client, factory);
    }

    /// <summary>
    /// Reads retained emp/registry/# messages and returns the connectionId of the first
    /// participant whose "type" field matches <paramref name="participantType"/>.
    /// Throws if none found within <paramref name="timeoutMs"/>.
    /// </summary>
    public async Task<string> FindConnectionAsync(string participantType, int timeoutMs = 3000)
    {
        var found = new TaskCompletionSource<string>();
        var seen  = new ConcurrentDictionary<string, bool>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            var topic = e.ApplicationMessage.Topic;
            if (!topic.StartsWith("emp/registry/")) return Task.CompletedTask;
            var seg = e.ApplicationMessage.PayloadSegment;
            if (seg.Count == 0) return Task.CompletedTask;
            var json = Encoding.UTF8.GetString(seg.Array!, seg.Offset, seg.Count);
            try
            {
                var obj = JObject.Parse(json);
                if (obj["type"]?.Value<string>() == participantType)
                {
                    // connectionId is the last segment of the topic
                    var connId = topic.Substring("emp/registry/".Length);
                    found.TrySetResult(connId);
                }
            }
            catch { }
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += onMessage;
        try
        {
            var subOptions = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic("emp/registry/#"))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => found.TrySetException(
                new TimeoutException($"No emp/ participant of type '{participantType}' found within {timeoutMs}ms")));

            return await found.Task;
        }
        finally
        {
            _client.ApplicationMessageReceivedAsync -= onMessage;
        }
    }

    /// <summary>
    /// Sends an emp/ request and awaits the response via MQTT5 ResponseTopic + CorrelationData.
    /// Publishes to: emp/{targetType}/{targetConnId}/{scope}/req/{operation}
    /// Response arrives on: emp/agent/{testConnId}/{scope}/res/{cid}
    /// </summary>
    public async Task<JObject> RequestAsync(
        string targetConnId,
        string targetType,
        string scope,
        string operation,
        string requestJson,
        int timeoutMs = 5000)
    {
        string cid          = Guid.NewGuid().ToString("N").Substring(0, 4);
        string responseTopic = $"emp/{TestParticipantType}/{_testConnId}/{scope}/res/{cid}";
        byte[] correlationData = Encoding.UTF8.GetBytes(cid);

        var tcs = new TaskCompletionSource<JObject>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            if (e.ApplicationMessage.Topic != responseTopic) return Task.CompletedTask;
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
        };

        _client.ApplicationMessageReceivedAsync += onMessage;
        try
        {
            var subOptions = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(responseTopic))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"emp/{targetType}/{targetConnId}/{scope}/req/{operation}")
                .WithPayload(requestJson)
                .WithResponseTopic(responseTopic)
                .WithCorrelationData(correlationData)
                .Build();
            await _client.PublishAsync(message, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => tcs.TrySetException(
                new TimeoutException($"emp/{scope}/req/{operation}: no response within {timeoutMs}ms")));

            return await tcs.Task;
        }
        finally
        {
            _client.ApplicationMessageReceivedAsync -= onMessage;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
    }
}
