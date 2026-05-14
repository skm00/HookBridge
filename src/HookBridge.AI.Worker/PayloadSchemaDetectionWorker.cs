using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.PayloadSchemaDetection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class PayloadSchemaDetectionWorker : BackgroundService
{
    private readonly IPayloadSchemaDetectionConsumer _consumer;
    private readonly IPayloadSchemaDetectionAgent _agent;
    private readonly IPayloadSchemaDetectionRepository _repository;
    private readonly ILogger<PayloadSchemaDetectionWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public PayloadSchemaDetectionWorker(
        IPayloadSchemaDetectionConsumer consumer,
        IPayloadSchemaDetectionAgent agent,
        IPayloadSchemaDetectionRepository repository,
        ILogger<PayloadSchemaDetectionWorker> logger,
        IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.PayloadSchemaDetectionTopic))
        {
            _logger.LogInformation("Payload schema detection topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["EventId"] = request.EventId,
                ["CorrelationId"] = request.CorrelationId
            });

            var response = await _agent.DetectAsync(request, stoppingToken);
            var result = PayloadSchemaDetectionResult.FromResponse(response, request);
            await _repository.InsertAsync(result, stoppingToken);

            _logger.LogInformation(
                "Payload schema detection completed. EventId: {EventId}, CorrelationId: {CorrelationId}, DetectedSchemaName: {DetectedSchemaName}, DetectedEventType: {DetectedEventType}, ConfidenceScore: {ConfidenceScore}, RiskLevel: {RiskLevel}, FallbackUsed: {FallbackUsed}",
                response.EventId,
                response.CorrelationId,
                response.DetectedSchemaName,
                response.DetectedEventType,
                response.ConfidenceScore,
                response.RiskLevel,
                response.Fallback?.UsedFallback ?? false);
        }
    }
}
