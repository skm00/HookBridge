using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiAnalysisConsumer : IAiAnalysisConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<AiAnalysisConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public AiAnalysisConsumer(
        IOptions<AiKafkaOptions> options,
        ILogger<AiAnalysisConsumer> logger,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? BuildConsumer;
    }

    public async IAsyncEnumerable<AiAnalysisEventDto> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.AiAnalysisTopic);

        _logger.LogInformation(
            "AI analysis Kafka consumer subscribed. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}",
            _options.AiAnalysisTopic,
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
                    "AI analysis Kafka consume error. Topic: {Topic}, Reason: {Reason}",
                    _options.AiAnalysisTopic,
                    ex.Error.Reason);
                continue;
            }

            if (consumeResult is null)
            {
                continue;
            }

            AiAnalysisEventDto? analysisEvent;
            try
            {
                analysisEvent = JsonSerializer.Deserialize<AiAnalysisEventDto>(
                    consumeResult.Message.Value,
                    AiAnalysisProducer.SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid AI analysis event JSON. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            if (analysisEvent is null)
            {
                _logger.LogWarning(
                    "AI analysis event JSON deserialized to null. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            _logger.LogInformation(
                "AI analysis event consumed. Topic: {Topic}, Key: {Key}, EventId: {EventId}, CorrelationId: {CorrelationId}, Source: {Source}, EventType: {EventType}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Message.Key,
                analysisEvent.EventId,
                analysisEvent.CorrelationId,
                analysisEvent.Source,
                analysisEvent.EventType,
                consumeResult.Partition,
                consumeResult.Offset);

            if (!_options.EnableAutoCommit)
            {
                CommitOffset(consumer, consumeResult);
            }

            yield return analysisEvent;
            await Task.Yield();
        }
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> consumeResult)
    {
        try
        {
            consumer.Commit(consumeResult);
            _logger.LogInformation(
                "AI analysis Kafka offset commit succeeded. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Partition,
                consumeResult.Offset);
        }
        catch (KafkaException ex)
        {
            _logger.LogError(
                ex,
                "AI analysis Kafka offset commit failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Partition,
                consumeResult.Offset);
        }
    }

    private static IConsumer<string, string> BuildConsumer(ConsumerConfig config)
        => new ConsumerBuilder<string, string>(config).Build();
}
