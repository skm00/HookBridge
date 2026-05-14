using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class WebhookDuplicateReplayDetectionWorker : BackgroundService
{
    private readonly IWebhookDuplicateReplayDetectionConsumer _consumer;
    private readonly IWebhookDuplicateReplayDetectionService _detectionService;
    private readonly IWebhookEventFingerprintRepository _repository;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<WebhookDuplicateReplayDetectionWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;
    private readonly DuplicateReplayDetectionOptions _options;

    public WebhookDuplicateReplayDetectionWorker(IWebhookDuplicateReplayDetectionConsumer consumer, IWebhookDuplicateReplayDetectionService detectionService, IWebhookEventFingerprintRepository repository, IAiAnomalyProducer anomalyProducer, ILogger<WebhookDuplicateReplayDetectionWorker> logger, IOptions<AiKafkaOptions> kafkaOptions, IOptions<DuplicateReplayDetectionOptions> options)
    {
        _consumer = consumer;
        _detectionService = detectionService;
        _repository = repository;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.DuplicateReplayDetectionTopic))
        {
            _logger.LogInformation("Webhook duplicate/replay detection topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            var request = message.Request;
            var response = await _detectionService.DetectAsync(request, stoppingToken);

            var exactDuplicate = response.DuplicateReason == WebhookDuplicateReplayReason.SameEventId;
            var shouldStore = response.SuggestedAction is WebhookDuplicateReplaySuggestedAction.Allow or WebhookDuplicateReplaySuggestedAction.Monitor;
            if (shouldStore && !exactDuplicate)
            {
                await _repository.InsertAsync(WebhookDuplicateReplayDetectionService.CreateFingerprint(request, response, _options), stoppingToken);
            }

            if (response.IsReplay || response.RiskLevel is AiRiskLevel.High or AiRiskLevel.Critical)
            {
                var anomalyEvent = new AiAnomalyEventDto
                {
                    AnomalyId = $"dup-replay-{Guid.NewGuid():N}",
                    EventId = response.EventId,
                    CorrelationId = response.CorrelationId,
                    CustomerId = response.CustomerId ?? string.Empty,
                    CustomerIdType = request.CustomerIdType,
                    SubscriptionId = response.SubscriptionId,
                    EndpointId = response.EndpointId,
                    TargetUrl = request.TargetUrl,
                    Environment = request.Environment,
                    EventType = request.EventType,
                    AnomalyType = AiAnomalyType.Unknown,
                    RiskLevel = response.RiskLevel,
                    AnomalyScore = response.DetectionScore,
                    Summary = response.Summary,
                    Recommendation = response.Recommendation,
                    Source = "HookBridge.AI.Worker.DuplicateReplayDetection",
                    CreatedAtUtc = response.DetectedAtUtc
                };
                var publishResult = await _anomalyProducer.PublishAsync(anomalyEvent, stoppingToken);
                if (!publishResult.IsSuccess) _logger.LogWarning("Duplicate/replay anomaly publish failed. Topic: {Topic}, Key: {Key}, ErrorMessage: {ErrorMessage}", publishResult.Topic, publishResult.Key, publishResult.ErrorMessage);
            }

            await message.AcknowledgeAsync(stoppingToken);
            _logger.LogInformation("Webhook duplicate/replay detection completed. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, SubscriptionId: {SubscriptionId}, EndpointId: {EndpointId}, IsDuplicate: {IsDuplicate}, IsReplay: {IsReplay}, DetectionScore: {DetectionScore}, RiskLevel: {RiskLevel}, SuggestedAction: {SuggestedAction}", response.EventId, response.CorrelationId, response.CustomerId, response.SubscriptionId, response.EndpointId, response.IsDuplicate, response.IsReplay, response.DetectionScore, response.RiskLevel, response.SuggestedAction);
        }
    }
}
