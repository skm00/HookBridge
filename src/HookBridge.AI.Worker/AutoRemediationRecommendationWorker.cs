using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.AutoRemediationRecommendation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class AutoRemediationRecommendationWorker : BackgroundService
{
    private readonly IAutoRemediationRecommendationConsumer _consumer;
    private readonly IAutoRemediationRecommendationService _service;
    private readonly IAutoRemediationRecommendationRepository _repository;
    private readonly IAiDecisionAuditService? _auditService;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<AutoRemediationRecommendationWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public AutoRemediationRecommendationWorker(IAutoRemediationRecommendationConsumer consumer, IAutoRemediationRecommendationService service, IAutoRemediationRecommendationRepository repository, IAiRecommendationApprovalService approvalService, IAiAnomalyProducer anomalyProducer, ILogger<AutoRemediationRecommendationWorker> logger, IOptions<AiKafkaOptions> kafkaOptions, IAiDecisionAuditService? auditService = null)
    {
        _consumer = consumer;
        _service = service;
        _repository = repository;
        _approvalService = approvalService;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _auditService = auditService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.AutoRemediationTopic))
        {
            _logger.LogInformation("Auto-remediation recommendation topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            try
            {
                var response = await _service.RecommendAsync(request, stoppingToken);
                await _repository.InsertAsync(AutoRemediationRecommendationResult.FromResponse(response, request), stoppingToken);
                if (_auditService is not null) await _auditService.AuditAutoRemediationRecommendationAsync(AiDecisionAuditRequestFactory.FromAutoRemediation(response, request), stoppingToken);
                _logger.LogInformation("Result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, RemediationType: {RemediationType}, RecommendedAction: {RecommendedAction}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.RemediationType, response.RecommendedAction, response.RiskLevel, response.RequiresApproval);

                if (response.RequiresApproval)
                {
                    await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                    {
                        RecommendationId = $"auto-remediation:{response.EventId}:{response.GeneratedAtUtc:O}",
                        EventId = response.EventId,
                        CorrelationId = response.CorrelationId,
                        CustomerId = request.CustomerId,
                        SubscriptionId = request.SubscriptionId,
                        EndpointId = request.EndpointId,
                        RecommendationType = MapRecommendationType(response.RemediationType),
                        RiskLevel = response.RiskLevel,
                        SuggestedAction = response.RecommendedAction.ToString(),
                        Summary = response.Summary,
                        Recommendation = response.Recommendation,
                        RequestedBy = "HookBridge.AI.AutoRemediationRecommendation"
                    }, stoppingToken);
                }

                if (!string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic) && response.RiskLevel is "High" or "Critical")
                {
                    await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
                }
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Invalid request. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
            }
        }
    }

    private static AiRecommendationType MapRecommendationType(AutoRemediationType remediationType) => remediationType switch
    {
        AutoRemediationType.SecurityQuarantineRecommendation => AiRecommendationType.SecurityRecommendation,
        AutoRemediationType.DeadLetterReview => AiRecommendationType.DeadLetterRecommendation,
        AutoRemediationType.EndpointPauseRecommendation or AutoRemediationType.EndpointResumeRecommendation => AiRecommendationType.EndpointRiskRecommendation,
        _ => AiRecommendationType.AnomalyRecommendation
    };

    private static AiAnomalyEventDto ToAnomalyEvent(AutoRemediationRecommendationResponseDto response, AutoRemediationRecommendationRequestDto request) => new()
    {
        AnomalyId = $"auto_remediation_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = AiAnomalyType.FailureSpike,
        RiskLevel = Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var risk) ? risk : AiRiskLevel.Unknown,
        AnomalyScore = response.RiskLevel == "Critical" ? 95 : 80,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.AutoRemediationRecommendation",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
