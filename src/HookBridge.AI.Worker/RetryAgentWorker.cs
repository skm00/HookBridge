using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.RetryAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class RetryAgentWorker : BackgroundService
{
    private readonly IRetryAgentConsumer _consumer;
    private readonly IRetryAgent _agent;
    private readonly IRetryAgentResultRepository _repository;
    private readonly IAiDecisionAuditService? _auditService;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<RetryAgentWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public RetryAgentWorker(IRetryAgentConsumer consumer, IRetryAgent agent, IRetryAgentResultRepository repository, IAiRecommendationApprovalService approvalService, IAiAnomalyProducer anomalyProducer, ILogger<RetryAgentWorker> logger, IOptions<AiKafkaOptions> kafkaOptions, IAiDecisionAuditService? auditService = null)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _approvalService = approvalService;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _auditService = auditService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.RetryAgentTopic))
        {
            _logger.LogInformation("Retry agent topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            var response = await _agent.AnalyzeAsync(request, stoppingToken);
            await _repository.InsertAsync(RetryAgentResult.FromResponse(response, request), stoppingToken);
            if (_auditService is not null) await _auditService.AuditRetryDecisionAsync(AiDecisionAuditRequestFactory.FromRetry(response, request), stoppingToken);
            _logger.LogInformation("Retry agent result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, RetryDecision: {RetryDecision}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.RetryDecision, response.RiskLevel, response.RequiresApproval);

            if (response.RequiresApproval)
            {
                await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                {
                    RecommendationId = $"retry-agent:{response.EventId}:{response.GeneratedAtUtc:O}",
                    EventId = response.EventId,
                    CorrelationId = response.CorrelationId,
                    CustomerId = request.CustomerId,
                    SubscriptionId = request.SubscriptionId,
                    EndpointId = request.EndpointId,
                    RecommendationType = AiRecommendationType.RetryRecommendation,
                    RiskLevel = response.RiskLevel,
                    SuggestedAction = response.RetryDecision.ToString(),
                    Summary = response.Summary,
                    Recommendation = response.Recommendation,
                    RequestedBy = "HookBridge.AI.RetryAgent"
                }, stoppingToken);
            }

            if (!string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic) && (response.RetryDecision == RetryAgentDecision.PauseEndpoint || response.RiskLevel is "High" or "Critical"))
            {
                await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
            }
        }
    }

    private static AiAnomalyEventDto ToAnomalyEvent(RetryAgentResponseDto response, RetryAgentRequestDto request) => new()
    {
        AnomalyId = $"retry_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        TargetUrl = request.TargetUrl,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = AiAnomalyType.FailureSpike,
        RiskLevel = Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var risk) ? risk : AiRiskLevel.Unknown,
        AnomalyScore = response.RiskLevel == "Critical" ? 95 : 80,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.RetryAgent",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
