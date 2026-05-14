using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class AiSecurityAnalysisWorker : BackgroundService
{
    private readonly IAiSecurityAnalysisConsumer _consumer;
    private readonly IAiSecurityAnalysisAgent _agent;
    private readonly IAiSecurityAnalysisRepository _repository;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<AiSecurityAnalysisWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public AiSecurityAnalysisWorker(IAiSecurityAnalysisConsumer consumer, IAiSecurityAnalysisAgent agent, IAiSecurityAnalysisRepository repository, IAiAnomalyProducer anomalyProducer, ILogger<AiSecurityAnalysisWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.SecurityAnalysisTopic))
        {
            _logger.LogInformation("AI security analysis topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            var response = await _agent.AnalyzeAsync(request, stoppingToken);
            await _repository.InsertAsync(AiSecurityAnalysisResult.FromResponse(response, request), stoppingToken);

            if (response.IsSuspicious && !string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic))
            {
                await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
            }

            _logger.LogInformation("AI security analysis completed. EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, IsSuspicious: {IsSuspicious}, SecurityRiskScore: {SecurityRiskScore}, RiskLevel: {RiskLevel}, SuggestedAction: {SuggestedAction}, FallbackUsed: {FallbackUsed}", response.EventId, response.CorrelationId, request.CustomerId, response.IsSuspicious, response.SecurityRiskScore, response.RiskLevel, response.SuggestedAction, response.Fallback?.UsedFallback ?? false);
        }
    }

    private static AiAnomalyEventDto ToAnomalyEvent(AiSecurityAnalysisResponseDto response, AiSecurityAnalysisRequestDto request) => new()
    {
        AnomalyId = $"sec_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        TargetUrl = request.TargetUrl,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = AiAnomalyType.SuspiciousPayloadSpike,
        RiskLevel = response.RiskLevel,
        AnomalyScore = response.SecurityRiskScore,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.SecurityAnalysis",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
