using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AiDecisionEventProducer : IAiDecisionEventProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<AiDecisionEventProducer> _logger;
    private readonly AiKafkaOptions _options;
    private bool _disposed;

    public AiDecisionEventProducer(IOptions<AiKafkaOptions> options, ILogger<AiDecisionEventProducer> logger, Func<ProducerConfig, IProducer<string, string>>? producerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _producer = (producerFactory ?? BuildProducer)(AiKafkaConfigFactory.CreateProducerConfig(_options));
    }

    public async Task<AiKafkaPublishResult> PublishAsync(AiDecisionEventDto decisionEvent, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(decisionEvent);
        var topic = _options.AiDecisionsTopic;
        var key = SelectMessageKey(decisionEvent);
        var publishedAtUtc = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(topic))
        {
            return AiKafkaPublishResult.Failure(topic, key, "AiKafka:AiDecisionsTopic is required when Kafka publishing is enabled.", publishedAtUtc);
        }

        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(decisionEvent, new ValidationContext(decisionEvent), validationResults, validateAllProperties: true))
        {
            var message = string.Join("; ", validationResults.Select(result => result.ErrorMessage));
            _logger.LogWarning("AI decision event publish failed validation. Topic: {Topic}, Key: {Key}, DecisionId: {DecisionId}, EventId: {EventId}, CorrelationId: {CorrelationId}, Reason: {Reason}", topic, key, decisionEvent.DecisionId, decisionEvent.EventId, decisionEvent.CorrelationId, message);
            return AiKafkaPublishResult.Failure(topic, key, message, publishedAtUtc);
        }

        _logger.LogInformation("AI decision event publish started. Topic: {Topic}, Key: {Key}, DecisionId: {DecisionId}, EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}, AgentName: {AgentName}", topic, key, decisionEvent.DecisionId, decisionEvent.EventId, decisionEvent.CorrelationId, decisionEvent.DecisionType, decisionEvent.AgentName);

        try
        {
            var payload = JsonSerializer.Serialize(decisionEvent, AiAnalysisProducer.SerializerOptions);
            var deliveryResult = await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = payload }, cancellationToken);
            _logger.LogInformation("AI decision event published. Topic: {Topic}, Key: {Key}, DecisionId: {DecisionId}, EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}, AgentName: {AgentName}, Partition: {Partition}, Offset: {Offset}", topic, key, decisionEvent.DecisionId, decisionEvent.EventId, decisionEvent.CorrelationId, decisionEvent.DecisionType, decisionEvent.AgentName, deliveryResult.Partition, deliveryResult.Offset);
            return AiKafkaPublishResult.Success(topic, key, deliveryResult.Partition.Value, deliveryResult.Offset.Value, publishedAtUtc);
        }
        catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or OperationCanceledException)
        {
            var reason = ex is ProduceException<string, string> produceException ? produceException.Error.Reason : ex.Message;
            _logger.LogError(ex, "AI decision event publish failed. Topic: {Topic}, Key: {Key}, DecisionId: {DecisionId}, EventId: {EventId}, CorrelationId: {CorrelationId}, DecisionType: {DecisionType}, AgentName: {AgentName}, Reason: {Reason}", topic, key, decisionEvent.DecisionId, decisionEvent.EventId, decisionEvent.CorrelationId, decisionEvent.DecisionType, decisionEvent.AgentName, reason);
            return AiKafkaPublishResult.Failure(topic, key, reason, publishedAtUtc);
        }
    }

    internal static string SelectMessageKey(AiDecisionEventDto decisionEvent)
        => !string.IsNullOrWhiteSpace(decisionEvent.CorrelationId) ? decisionEvent.CorrelationId! : !string.IsNullOrWhiteSpace(decisionEvent.EventId) ? decisionEvent.EventId! : decisionEvent.DecisionId;

    private static IProducer<string, string> BuildProducer(ProducerConfig config) => new ProducerBuilder<string, string>(config).Build();

    public void Dispose()
    {
        if (_disposed) return;
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
    }
}
