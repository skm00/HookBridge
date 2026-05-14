using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class WebhookTransformationRecommendationWorker : BackgroundService
{
    private readonly IWebhookTransformationRecommendationConsumer _consumer;
    private readonly IWebhookTransformationRecommendationAgent _agent;
    private readonly IWebhookTransformationRecommendationRepository _repository;
    private readonly ILogger<WebhookTransformationRecommendationWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public WebhookTransformationRecommendationWorker(IWebhookTransformationRecommendationConsumer consumer, IWebhookTransformationRecommendationAgent agent, IWebhookTransformationRecommendationRepository repository, ILogger<WebhookTransformationRecommendationWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.WebhookTransformationRecommendationTopic))
        {
            _logger.LogInformation("Webhook transformation recommendation topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            var request = message.Request;
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId });
            var response = await _agent.RecommendAsync(request, stoppingToken);
            await _repository.InsertAsync(WebhookTransformationRecommendationResult.FromResponse(response, request), stoppingToken);
            await message.AcknowledgeAsync(stoppingToken);
            _logger.LogInformation(
                "Webhook transformation recommendation completed. EventId: {EventId}, CorrelationId: {CorrelationId}, MappingCount: {MappingCount}, MissingTargetCount: {MissingTargetCount}, UnmappedSourceCount: {UnmappedSourceCount}, ConfidenceScore: {ConfidenceScore}, RiskLevel: {RiskLevel}, FallbackUsed: {FallbackUsed}",
                response.EventId,
                response.CorrelationId,
                response.RecommendedMappings.Count,
                response.MissingTargetFields.Count,
                response.UnmappedSourceFields.Count,
                response.ConfidenceScore,
                response.RiskLevel,
                response.Fallback?.UsedFallback ?? false);
        }
    }
}
