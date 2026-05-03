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
/// MQTT5-native test client for the ESB/ topic schema.
/// Uses ResponseTopic + CorrelationData for request/reply rather than topic substitution.
///
/// Usage:
///   await using var mqtt = await SBTestClient.ConnectAsync();
///   string connId = await mqtt.FindConnectionAsync("Client");
///   var payload = await mqtt.RequestAsync(connId, "Client", "App", "GameTicks", "{}");
///   Assert.NotNull(payload["GameTicks"]);
/// </summary>
public sealed class SBTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory;
    private SBTestClient(IMqttClient client, MqttFactory factory)
    {
        _client  = client;
        _factory = factory;
    }

    public static async Task<SBTestClient> ConnectAsync()
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
        return new SBTestClient(client, factory);
    }

    /// <summary>
    /// Reads retained ESB/Registry/# messages and returns the connectionId of the first
    /// participant whose "type" field matches <paramref name="participantType"/>.
    /// Throws if none found within <paramref name="timeoutMs"/>.
    /// </summary>
    public async Task<string> FindConnectionAsync(string participantType, int timeoutMs = 3000)
    {
        var found = new TaskCompletionSource<string>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            var topic = e.ApplicationMessage.Topic;
            if (!topic.StartsWith("ESB/Registry/")) return Task.CompletedTask;
            var seg = e.ApplicationMessage.PayloadSegment;
            if (seg.Count == 0) return Task.CompletedTask;
            var json = Encoding.UTF8.GetString(seg.Array!, seg.Offset, seg.Count);
            try
            {
                var obj = JObject.Parse(json);
                if (obj["type"]?.Value<string>() == participantType)
                {
                    var connId = topic.Substring("ESB/Registry/".Length);
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
                .WithTopicFilter(f => f.WithTopic("ESB/Registry/#"))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => found.TrySetException(
                new TimeoutException($"No ESB participant of type '{participantType}' found within {timeoutMs}ms")));

            return await found.Task;
        }
        finally
        {
            _client.ApplicationMessageReceivedAsync -= onMessage;
        }
    }

    /// <summary>
    /// Sends an ESB/ request and awaits the response.
    /// Publishes to: ESB/{targetType}/{targetConnId}/{scope}/Req/{operation}
    /// Response arrives on: ESB/{targetType}/{targetConnId}/{scope}/Res/{operation}
    /// Errors arrive on:    ESB/{targetType}/{targetConnId}/{scope}/Err/{operation}
    /// </summary>
    public async Task<JObject> RequestAsync(
        string targetConnId,
        string targetType,
        string scope,
        string operation,
        string requestJson,
        int timeoutMs = 5000)
    {
        string responseTopic = $"ESB/{targetType}/{targetConnId}/{scope}/Res/{operation}";
        string errorTopic    = $"ESB/{targetType}/{targetConnId}/{scope}/Err/{operation}";

        var tcs = new TaskCompletionSource<JObject>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            var topic = e.ApplicationMessage.Topic;
            if (topic != responseTopic && topic != errorTopic) return Task.CompletedTask;
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
                .WithTopicFilter(f => f.WithTopic(errorTopic))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"ESB/{targetType}/{targetConnId}/{scope}/Req/{operation}")
                .WithPayload(requestJson)
                .Build();
            await _client.PublishAsync(message, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() => tcs.TrySetException(
                new TimeoutException($"ESB/Req/{scope}/{operation}: no response within {timeoutMs}ms")));

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
