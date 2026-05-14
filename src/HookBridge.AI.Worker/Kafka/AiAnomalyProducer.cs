using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiAnomalyProducer : IAiAnomalyProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<AiAnomalyProducer> _logger;
    private readonly AiKafkaOptions _options;
    private bool _disposed;

    public AiAnomalyProducer(
        IOptions<AiKafkaOptions> options,
        ILogger<AiAnomalyProducer> logger,
        Func<ProducerConfig, IProducer<string, string>>? producerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _producer = (producerFactory ?? BuildProducer)(AiKafkaConfigFactory.CreateProducerConfig(_options));
    }

    public async Task<AiKafkaPublishResult> PublishAsync(AiAnomalyEventDto anomalyEvent, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(anomalyEvent);

        var topic = _options.AnomaliesTopic;
        var key = SelectMessageKey(anomalyEvent);
        var payload = JsonSerializer.Serialize(anomalyEvent, AiAnalysisProducer.SerializerOptions);
        var publishedAtUtc = DateTime.UtcNow;

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
                "AI anomaly event published. Topic: {Topic}, Key: {Key}, AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, AnomalyType: {AnomalyType}, RiskLevel: {RiskLevel}, Partition: {Partition}, Offset: {Offset}",
                topic,
                key,
                anomalyEvent.AnomalyId,
                anomalyEvent.EventId,
                anomalyEvent.CorrelationId,
                anomalyEvent.AnomalyType,
                anomalyEvent.RiskLevel,
                deliveryResult.Partition,
                deliveryResult.Offset);

            return AiKafkaPublishResult.Success(topic, key, deliveryResult.Partition.Value, deliveryResult.Offset.Value, publishedAtUtc);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or OperationCanceledException)
        {
            var reason = ex is ProduceException<string, string> produceException
                ? produceException.Error.Reason
                : ex.Message;

            _logger.LogError(
                ex,
                "AI anomaly event publish failed. Topic: {Topic}, Key: {Key}, AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}",
                topic,
                key,
                anomalyEvent.AnomalyId,
                anomalyEvent.EventId,
                anomalyEvent.CorrelationId,
                reason);

            return AiKafkaPublishResult.Failure(topic, key, reason, publishedAtUtc);
        }
    }

    internal static string SelectMessageKey(AiAnomalyEventDto anomalyEvent)
        => !string.IsNullOrWhiteSpace(anomalyEvent.CorrelationId)
            ? anomalyEvent.CorrelationId!
            : anomalyEvent.AnomalyId;

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
