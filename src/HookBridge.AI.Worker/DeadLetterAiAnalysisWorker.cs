using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class DeadLetterAiAnalysisWorker : BackgroundService
{
    private readonly IDeadLetterAiAnalysisConsumer _consumer;
    private readonly IDeadLetterAiAnalysisService _service;
    private readonly IDeadLetterAiAnalysisRepository _repository;
    private readonly IHumanApprovalWorkflowService _approvalService;
    private readonly IAiDecisionEventProducer _decisionProducer;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly IAiDecisionAuditService? _auditService;
    private readonly ILogger<DeadLetterAiAnalysisWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public DeadLetterAiAnalysisWorker(IDeadLetterAiAnalysisConsumer consumer, IDeadLetterAiAnalysisService service, IDeadLetterAiAnalysisRepository repository, IHumanApprovalWorkflowService approvalService, IAiDecisionEventProducer decisionProducer, IAiAnomalyProducer anomalyProducer, ILogger<DeadLetterAiAnalysisWorker> logger, IOptions<AiKafkaOptions> kafkaOptions, IAiDecisionAuditService? auditService = null)
    {
        _consumer = consumer;
        _service = service;
        _repository = repository;
        _approvalService = approvalService;
        _decisionProducer = decisionProducer;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _auditService = auditService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.DeadLetterAiAnalysisTopic))
        {
            _logger.LogInformation("Dead-letter AI analysis topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["DeadLetterId"] = request.DeadLetterId, ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            try
            {
                var response = await _service.AnalyzeAsync(request, stoppingToken);
                await _repository.InsertAsync(DeadLetterAiAnalysisResult.FromResponse(response, request), stoppingToken);
                _logger.LogInformation("Result stored. DeadLetterId: {DeadLetterId}, EventId: {EventId}, ReplaySafety: {ReplaySafety}, SuggestedAction: {SuggestedAction}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.DeadLetterId, response.EventId, response.ReplaySafety, response.SuggestedAction, response.RiskLevel, response.RequiresApproval);

                if (response.RequiresApproval)
                {
                    await _approvalService.CreateAsync(new HumanApprovalWorkflowCreateRequestDto
                    {
                        RecommendationId = $"deadletter:{response.DeadLetterId}:{response.GeneratedAtUtc:O}",
                        RecommendationType = AiRecommendationType.DeadLetterRecommendation,
                        EventId = response.EventId,
                        CorrelationId = response.CorrelationId,
                        CustomerId = request.CustomerId,
                        CustomerIdType = request.CustomerIdType,
                        SubscriptionId = request.SubscriptionId,
                        EndpointId = request.EndpointId,
                        Environment = request.Environment,
                        RiskLevel = response.RiskLevel,
                        SuggestedAction = response.SuggestedAction.ToString(),
                        Summary = response.Summary,
                        Recommendation = response.Recommendation,
                        ConfidenceScore = response.ConfidenceScore,
                        ConfidenceLevel = response.ConfidenceLevel.ToString(),
                        RequestedBy = "HookBridge.AI.DeadLetterAiAnalysis"
                    }, stoppingToken);
                    _logger.LogInformation("Approval required. DeadLetterId: {DeadLetterId}, EventId: {EventId}, SuggestedAction: {SuggestedAction}", response.DeadLetterId, response.EventId, response.SuggestedAction);
                }

                if (_auditService is not null)
                {
                    await _auditService.AuditGenericDecisionAsync(ToAuditRequest(response, request), stoppingToken);
                    _logger.LogInformation("Audit record created. DeadLetterId: {DeadLetterId}, EventId: {EventId}", response.DeadLetterId, response.EventId);
                }

                await _decisionProducer.PublishAsync(ToDecisionEvent(response, request), stoppingToken);
                _logger.LogInformation("Decision event published. DeadLetterId: {DeadLetterId}, EventId: {EventId}", response.DeadLetterId, response.EventId);

                if (response.RiskLevel is "High" or "Critical" || request.IsReplay || request.IsSuspicious)
                {
                    await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
                }
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Invalid dead-letter analysis request. DeadLetterId: {DeadLetterId}, EventId: {EventId}", request.DeadLetterId, request.EventId);
            }
        }
    }

    private static AiDecisionAuditCreateRequestDto ToAuditRequest(DeadLetterAiAnalysisResponseDto response, DeadLetterAiAnalysisRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        AgentName = nameof(DeadLetterAiAnalysisWorker),
        DecisionType = AiDecisionAuditType.DeadLetterAnalysis,
        Decision = response.ReplaySafety.ToString(),
        RiskLevel = response.RiskLevel,
        ConfidenceScore = response.ConfidenceScore,
        ConfidenceLevel = response.ConfidenceLevel.ToString(),
        SuggestedAction = response.SuggestedAction.ToString(),
        RequiresApproval = response.RequiresApproval,
        SafeModeDecision = response.SafeModeDecision.ToString(),
        IsActionAllowed = response.IsActionAllowed,
        UsedAi = !response.Fallback.UsedFallback,
        UsedFallback = response.Fallback.UsedFallback,
        FallbackReason = response.Fallback.FallbackReason.ToString(),
        PromptName = response.PromptName,
        PromptVersion = response.PromptVersion,
        PromptHash = response.PromptHash,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList(),
    };

    private static AiDecisionEventDto ToDecisionEvent(DeadLetterAiAnalysisResponseDto response, DeadLetterAiAnalysisRequestDto request) => new()
    {
        DecisionId = $"deadletter:{response.DeadLetterId}:{response.GeneratedAtUtc:O}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        AgentName = nameof(DeadLetterAiAnalysisWorker),
        DecisionType = AiDecisionEventType.DeadLetterAnalysis,
        Decision = response.ReplaySafety.ToString(),
        RiskLevel = response.RiskLevel,
        ConfidenceScore = response.ConfidenceScore,
        ConfidenceLevel = response.ConfidenceLevel.ToString(),
        SuggestedAction = response.SuggestedAction.ToString(),
        RequiresApproval = response.RequiresApproval,
        SafeModeDecision = response.SafeModeDecision.ToString(),
        IsActionAllowed = response.IsActionAllowed,
        UsedAi = !response.Fallback.UsedFallback,
        UsedFallback = response.Fallback.UsedFallback,
        FallbackReason = response.Fallback.FallbackReason.ToString(),
        PromptName = response.PromptName,
        PromptVersion = response.PromptVersion,
        PromptHash = response.PromptHash,
        Model = response.Model,
        Provider = response.Provider,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList(),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static AiAnomalyEventDto ToAnomalyEvent(DeadLetterAiAnalysisResponseDto response, DeadLetterAiAnalysisRequestDto request) => new()
    {
        AnomalyId = $"deadletter_{response.DeadLetterId}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = request.IsSuspicious ? AiAnomalyType.SuspiciousPayloadSpike : AiAnomalyType.FailureSpike,
        RiskLevel = Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var risk) ? risk : AiRiskLevel.Unknown,
        AnomalyScore = response.RiskLevel == "Critical" ? 95 : 80,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.DeadLetterAiAnalysis",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
