using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiDecisionEventConsumer : IAiDecisionEventConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<AiDecisionEventConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public AiDecisionEventConsumer(IOptions<AiKafkaOptions> options, ILogger<AiDecisionEventConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? BuildConsumer;
    }

    public async IAsyncEnumerable<AiDecisionEventDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.AiDecisionsTopic);
        _logger.LogInformation("AI decision event Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.AiDecisionsTopic, _options.ConsumerGroupId);

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
                _logger.LogError(ex, "AI decision event Kafka consume error. Topic: {Topic}, Reason: {Reason}", _options.AiDecisionsTopic, ex.Error.Reason);
                continue;
            }

            if (consumeResult is null) continue;

            AiDecisionEventDto? decisionEvent;
            try
            {
                decisionEvent = JsonSerializer.Deserialize<AiDecisionEventDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid decision event skipped. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}", consumeResult.Topic, _options.ConsumerGroupId, consumeResult.Message.Key, consumeResult.Partition, consumeResult.Offset);
                continue;
            }

            if (decisionEvent is null || string.IsNullOrWhiteSpace(decisionEvent.DecisionId))
            {
                _logger.LogWarning("Invalid decision event skipped. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}, Key: {Key}, Partition: {Partition}, Offset: {Offset}", consumeResult.Topic, _options.ConsumerGroupId, consumeResult.Message.Key, consumeResult.Partition, consumeResult.Offset);
                continue;
            }

            _logger.LogInformation("AI decision event consumed. KafkaTopic: {KafkaTopic}, ConsumerGroupId: {ConsumerGroupId}, DecisionId: {DecisionId}, AuditId: {AuditId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, DecisionType: {DecisionType}, AgentName: {AgentName}, Partition: {Partition}, Offset: {Offset}", consumeResult.Topic, _options.ConsumerGroupId, decisionEvent.DecisionId, decisionEvent.AuditId, decisionEvent.EventId, decisionEvent.CorrelationId, decisionEvent.CustomerId, decisionEvent.DecisionType, decisionEvent.AgentName, consumeResult.Partition, consumeResult.Offset);

            if (!_options.EnableAutoCommit)
            {
                CommitOffset(consumer, consumeResult);
            }

            yield return decisionEvent;
            await Task.Yield();
        }
    }

    private void CommitOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> consumeResult)
    {
        try
        {
            consumer.Commit(consumeResult);
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "AI decision event Kafka offset commit failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}", consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);
        }
    }

    private static IConsumer<string, string> BuildConsumer(ConsumerConfig config) => new ConsumerBuilder<string, string>(config).Build();
}
