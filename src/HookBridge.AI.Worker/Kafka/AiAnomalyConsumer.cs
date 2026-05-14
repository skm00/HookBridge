using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiAnomalyConsumer : IAiAnomalyConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<AiAnomalyConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public AiAnomalyConsumer(
        IOptions<AiKafkaOptions> options,
        ILogger<AiAnomalyConsumer> logger,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? BuildConsumer;
    }

    public async IAsyncEnumerable<AiAnomalyEventDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.AnomaliesTopic);

        _logger.LogInformation(
            "AI anomaly Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}",
            _options.AnomaliesTopic,
            _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try
            {
                consumeResult = consumer.Consume(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(
                    ex,
                    "AI anomaly Kafka consume error. Topic: {Topic}, Reason: {Reason}",
                    _options.AnomaliesTopic,
                    ex.Error.Reason);
                continue;
            }

            if (consumeResult is null)
            {
                continue;
            }

            AiAnomalyEventDto? anomalyEvent;
            try
            {
                anomalyEvent = JsonSerializer.Deserialize<AiAnomalyEventDto>(
                    consumeResult.Message.Value,
                    AiAnalysisProducer.SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid AI anomaly Kafka message skipped. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    _options.ConsumerGroupId,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            if (anomalyEvent is null)
            {
                _logger.LogWarning(
                    "Invalid AI anomaly Kafka message skipped. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    _options.ConsumerGroupId,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            _logger.LogInformation(
                "AI anomaly event consumed. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, AnomalyType: {AnomalyType}, RiskLevel: {RiskLevel}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                _options.ConsumerGroupId,
                anomalyEvent.AnomalyId,
                anomalyEvent.EventId,
                anomalyEvent.CorrelationId,
                anomalyEvent.CustomerId,
                anomalyEvent.AnomalyType,
                anomalyEvent.RiskLevel,
                consumeResult.Partition,
                consumeResult.Offset);

            if (!_options.EnableAutoCommit)
            {
                CommitOffset(consumer, consumeResult);
            }

            yield return anomalyEvent;
            await Task.Yield();
        }
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> consumeResult)
    {
        try
        {
            consumer.Commit(consumeResult);
            _logger.LogInformation(
                "AI anomaly Kafka offset commit succeeded. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Partition,
                consumeResult.Offset);
        }
        catch (KafkaException ex)
        {
            _logger.LogError(
                ex,
                "AI anomaly Kafka offset commit failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Partition,
                consumeResult.Offset);
        }
    }

    private static IConsumer<string, string> BuildConsumer(ConsumerConfig config)
        => new ConsumerBuilder<string, string>(config).Build();
}
