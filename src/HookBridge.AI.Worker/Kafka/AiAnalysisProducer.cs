using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiAnalysisProducer : IAiAnalysisProducer, IDisposable
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<AiAnalysisProducer> _logger;
    private readonly AiKafkaOptions _options;
    private bool _disposed;

    public AiAnalysisProducer(
        IOptions<AiKafkaOptions> options,
        ILogger<AiAnalysisProducer> logger,
        Func<ProducerConfig, IProducer<string, string>>? producerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _producer = (producerFactory ?? BuildProducer)(AiKafkaConfigFactory.CreateProducerConfig(_options));
    }

    public async Task<AiAnalysisPublishResult> PublishAsync(
        AiAnalysisEventDto analysisEvent,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(analysisEvent);

        var topic = _options.AiAnalysisTopic;
        var key = SelectMessageKey(analysisEvent);
        var payload = JsonSerializer.Serialize(analysisEvent, SerializerOptions);

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = key,
                    Value = payload,
                },
                cancellationToken);

            _logger.LogInformation(
                "AI analysis event published. Topic: {Topic}, Key: {Key}, EventId: {EventId}, CorrelationId: {CorrelationId}, Partition: {Partition}, Offset: {Offset}",
                topic,
                key,
                analysisEvent.EventId,
                analysisEvent.CorrelationId,
                deliveryResult.Partition,
                deliveryResult.Offset);

            return AiAnalysisPublishResult.Success(topic, key, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or OperationCanceledException)
        {
            var reason = ex is ProduceException<string, string> produceException
                ? produceException.Error.Reason
                : ex.Message;

            _logger.LogError(
                ex,
                "AI analysis event publish failed. Topic: {Topic}, Key: {Key}, EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                topic,
                key,
                analysisEvent.EventId,
                analysisEvent.CorrelationId,
                reason);

            return AiAnalysisPublishResult.Failure(topic, key, reason);
        }
    }

    internal static string SelectMessageKey(AiAnalysisEventDto analysisEvent)
        => !string.IsNullOrWhiteSpace(analysisEvent.CorrelationId)
            ? analysisEvent.CorrelationId!
            : analysisEvent.EventId;

    private static IProducer<string, string> BuildProducer(ProducerConfig config)
        => new ProducerBuilder<string, string>(config).Build();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
    }
}
