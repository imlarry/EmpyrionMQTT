using ESB.Configuration;
using ESB.Utilities;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESBTests.Infrastructure;

/// <summary>
/// Thin MQTT client for integration testing. Each test should create its own
/// instance (await using) so topic subscriptions don't bleed across tests.
/// Connection settings are read from ESB_Info.yaml in the test output directory.
///
/// Usage:
///   await using var mqtt = await MqttTestClient.ConnectAsync();
///   var (topic, payload) = await mqtt.RequestAsync("Structure.Info", "{\"EntityId\":5319}");
///   Assert.StartsWith("Client/R/", topic);
/// </summary>
public sealed class MqttTestClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttFactory _factory;

    private MqttTestClient(IMqttClient client, MqttFactory factory)
    {
        _client = client;
        _factory = factory;
    }

    public static async Task<MqttTestClient> ConnectAsync()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ESB_Info.yaml");
        var cfg = YamlFileReader.ReadYamlFile<ESBConfig>(configPath).MQTThost;

        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(cfg.WithTcpServer, cfg.Port)
            .WithProtocolVersion(MqttProtocolVersion.V500);

        if (!string.IsNullOrEmpty(cfg.Username))
            builder = builder.WithCredentials(cfg.Username, cfg.Password);

        await client.ConnectAsync(builder.Build(), CancellationToken.None);
        return new MqttTestClient(client, factory);
    }

    /// <summary>
    /// Publishes a request to <c>{appId}/Q/{handler}/*/&lt;seqId&gt;</c> and awaits the
    /// response on <c>{appId}/R/{handler}/*/&lt;seqId&gt;</c> or <c>{appId}/X/{handler}/*/&lt;seqId&gt;</c>.
    /// The sequence ID is unique per call so concurrent requests from different test
    /// classes to the same handler cannot receive each other's responses.
    /// Returns the full topic string and the parsed JSON payload.
    /// Throws <see cref="TimeoutException"/> if no response arrives within <paramref name="timeoutMs"/>.
    /// </summary>
    public async Task<(string Topic, JObject Payload)> RequestAsync(
        string handler,
        string requestJson,
        int timeoutMs = 5000,
        string appId = "Client")
    {
        // Unique sequence ID for this request — position[4] in the topic.
        // The ESB subscription Client/Q/+/*/#  covers this via the trailing #.
        // The response echoes back on the same topic with Q→R or Q→X, so
        // subscribing to our specific seqId means we only receive our own response.
        string seqId = Guid.NewGuid().ToString("N").Substring(0, 8);

        var tcs = new TaskCompletionSource<(string, JObject)>();

        Func<MqttApplicationMessageReceivedEventArgs, Task> onMessage = e =>
        {
            var t = e.ApplicationMessage.Topic;
            if (t.StartsWith($"{appId}/R/{handler}/") || t.StartsWith($"{appId}/X/{handler}/"))
            {
                var seg = e.ApplicationMessage.PayloadSegment;
                var json = Encoding.UTF8.GetString(seg.Array!, seg.Offset, seg.Count);
                // Some handlers return a bare JSON array; wrap it so callers always get JObject.
                // Access via payload["items"] for those responses.
                var token = JToken.Parse(json);
                var jobj = token as JObject ?? new JObject(new JProperty("items", token));
                tcs.TrySetResult((t, jobj));
            }
            return Task.CompletedTask;
        };

        _client.ApplicationMessageReceivedAsync += onMessage;

        try
        {
            // Subscribe to our unique seqId so the broker only delivers this
            // request's response here — no cross-talk between concurrent tests.
            var subOptions = _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic($"{appId}/R/{handler}/*/{seqId}/#"))
                .WithTopicFilter(f => f.WithTopic($"{appId}/X/{handler}/*/{seqId}/#"))
                .Build();
            await _client.SubscribeAsync(subOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"{appId}/Q/{handler}/*/{seqId}")
                .WithPayload(requestJson)
                .Build();
            await _client.PublishAsync(message, CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException($"{handler}: no response within {timeoutMs}ms")));

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
