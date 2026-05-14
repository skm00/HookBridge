using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class WebhookFailureAnomalyDetectionWorker : BackgroundService
{
    private readonly IWebhookFailureAnomalyDetectionConsumer _consumer;
    private readonly IWebhookFailureAnomalyDetectionService _detectionService;
    private readonly IWebhookFailureAnomalyDetectionRepository _repository;
    private readonly ILogger<WebhookFailureAnomalyDetectionWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public WebhookFailureAnomalyDetectionWorker(IWebhookFailureAnomalyDetectionConsumer consumer, IWebhookFailureAnomalyDetectionService detectionService, IWebhookFailureAnomalyDetectionRepository repository, ILogger<WebhookFailureAnomalyDetectionWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _detectionService = detectionService;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.WebhookFailureAnomalyDetectionTopic))
        {
            _logger.LogInformation("Webhook failure anomaly detection topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            var request = message.Request;
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CustomerId"] = request.CustomerId,
                ["SubscriptionId"] = request.SubscriptionId,
                ["EndpointId"] = request.EndpointId,
                ["EventType"] = request.EventType,
                ["Environment"] = request.Environment
            });

            var response = _detectionService.DetectAnomalies(request, DateTime.UtcNow);
            await _repository.InsertAsync(WebhookFailureAnomalyDetectionResult.FromResponse(response), stoppingToken);
            await message.AcknowledgeAsync(stoppingToken);

            _logger.LogInformation("Webhook failure anomaly detection completed. CustomerId: {CustomerId}, SubscriptionId: {SubscriptionId}, EndpointId: {EndpointId}, EventType: {EventType}, Environment: {Environment}, IsAnomalyDetected: {IsAnomalyDetected}, AnomalyScore: {AnomalyScore}, RiskLevel: {RiskLevel}, DetectedAnomalyCount: {DetectedAnomalyCount}", response.CustomerId, response.SubscriptionId, response.EndpointId, response.EventType, response.Environment, response.IsAnomalyDetected, response.AnomalyScore, response.RiskLevel, response.DetectedAnomalies.Count);
        }
    }
}
