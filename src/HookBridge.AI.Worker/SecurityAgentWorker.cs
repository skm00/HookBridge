using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SecurityAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class SecurityAgentWorker : BackgroundService
{
    private readonly ISecurityAgentConsumer _consumer;
    private readonly ISecurityAgent _agent;
    private readonly ISecurityAgentResultRepository _repository;
    private readonly IAiDecisionAuditService? _auditService;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<SecurityAgentWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;
    private readonly SecurityAgentOptions _securityOptions;

    public SecurityAgentWorker(ISecurityAgentConsumer consumer, ISecurityAgent agent, ISecurityAgentResultRepository repository, IAiRecommendationApprovalService approvalService, IAiAnomalyProducer anomalyProducer, ILogger<SecurityAgentWorker> logger, IOptions<AiKafkaOptions> kafkaOptions, IOptions<SecurityAgentOptions> securityOptions, IAiDecisionAuditService? auditService = null)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _approvalService = approvalService;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _securityOptions = securityOptions.Value;
        _auditService = auditService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.SecurityAgentTopic))
        {
            _logger.LogInformation("Security agent topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            try
            {
                var response = await _agent.AnalyzeAsync(request, stoppingToken);
                await _repository.InsertAsync(SecurityAgentResult.FromResponse(response, request), stoppingToken);
                if (_auditService is not null) await _auditService.AuditSecurityDecisionAsync(AiDecisionAuditRequestFactory.FromSecurity(response, request), stoppingToken);
                _logger.LogInformation("Result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, SecurityDecision: {SecurityDecision}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.SecurityDecision, response.RiskLevel, response.RequiresApproval);

                if (response.RequiresApproval)
                {
                    await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                    {
                        RecommendationId = $"security-agent:{response.EventId}:{response.GeneratedAtUtc:O}",
                        EventId = response.EventId,
                        CorrelationId = response.CorrelationId,
                        CustomerId = request.CustomerId,
                        SubscriptionId = request.SubscriptionId,
                        EndpointId = request.EndpointId,
                        RecommendationType = AiRecommendationType.SecurityRecommendation,
                        RiskLevel = response.RiskLevel.ToString(),
                        SuggestedAction = response.SecurityDecision.ToString(),
                        Summary = response.Summary,
                        Recommendation = response.Recommendation,
                        RequestedBy = "HookBridge.AI.SecurityAgent"
                    }, stoppingToken);
                    _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, SecurityDecision: {SecurityDecision}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.SecurityDecision, response.RiskLevel);
                }

                if (!string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic) && ShouldPublishAnomaly(response))
                {
                    await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
                    _logger.LogInformation("Anomaly published. EventId: {EventId}, CorrelationId: {CorrelationId}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.RiskLevel);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            }
        }
    }

    private bool ShouldPublishAnomaly(SecurityAgentResponseDto response)
        => response.RiskLevel == AiRiskLevel.High && _securityOptions.PublishAnomalyForHighRisk
           || response.RiskLevel == AiRiskLevel.Critical && _securityOptions.PublishAnomalyForCriticalRisk;

    private static AiAnomalyEventDto ToAnomalyEvent(SecurityAgentResponseDto response, SecurityAgentRequestDto request) => new()
    {
        AnomalyId = $"security_agent_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
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
        Source = "HookBridge.AI.SecurityAgent",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
