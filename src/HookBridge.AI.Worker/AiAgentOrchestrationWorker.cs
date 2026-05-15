using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class AiAgentOrchestrationWorker : BackgroundService
{
    private readonly IAiAgentOrchestrationConsumer _consumer;
    private readonly IAiAgentOrchestrator _orchestrator;
    private readonly IAiAgentOrchestrationRepository _repository;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly AiKafkaOptions _kafkaOptions;
    private readonly ILogger<AiAgentOrchestrationWorker> _logger;

    public AiAgentOrchestrationWorker(
        IAiAgentOrchestrationConsumer consumer,
        IAiAgentOrchestrator orchestrator,
        IAiAgentOrchestrationRepository repository,
        IAiRecommendationApprovalService approvalService,
        IAiAnomalyProducer anomalyProducer,
        IOptions<AiKafkaOptions> kafkaOptions,
        ILogger<AiAgentOrchestrationWorker> logger)
    {
        _consumer = consumer;
        _orchestrator = orchestrator;
        _repository = repository;
        _approvalService = approvalService;
        _anomalyProducer = anomalyProducer;
        _kafkaOptions = kafkaOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.OrchestrationTopic))
        {
            _logger.LogInformation("AI orchestration topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["EventId"] = request.EventId,
                ["CorrelationId"] = request.CorrelationId,
                ["CustomerId"] = request.CustomerId
            });

            var response = await _orchestrator.OrchestrateAsync(request, stoppingToken);
            if (response.RequiresApproval)
            {
                var approval = await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                {
                    RecommendationId = $"orchestration:{response.EventId}:{response.GeneratedAtUtc:O}",
                    EventId = response.EventId,
                    CorrelationId = response.CorrelationId,
                    CustomerId = request.CustomerId,
                    SubscriptionId = request.SubscriptionId,
                    EndpointId = request.EndpointId,
                    RecommendationType = AiRecommendationType.AnomalyRecommendation,
                    RiskLevel = response.OverallRiskLevel.ToString(),
                    SuggestedAction = response.RecommendedAction.ToString(),
                    Summary = response.OverallSummary,
                    Recommendation = response.RecommendedAction.ToString(),
                    RequestedBy = "HookBridge.AI.Orchestration"
                }, stoppingToken);
                response.ApprovalId = approval.Id;
                _logger.LogInformation("AI orchestration approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, ApprovalId: {ApprovalId}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.ApprovalId, response.OverallRiskLevel);
            }

            await _repository.InsertAsync(AiAgentOrchestrationResult.FromResponse(response, request), stoppingToken);

            if (response.OverallRiskLevel is AiRiskLevel.High or AiRiskLevel.Critical && !string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic))
            {
                await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
                _logger.LogInformation("AI orchestration anomaly published. EventId: {EventId}, CorrelationId: {CorrelationId}, RiskLevel: {RiskLevel}", response.EventId, response.CorrelationId, response.OverallRiskLevel);
            }
        }
    }

    private static AiAnomalyEventDto ToAnomalyEvent(AiAgentOrchestrationResponseDto response, AiAgentOrchestrationRequestDto request) => new()
    {
        AnomalyId = $"orch_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        TargetUrl = request.TargetUrl,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = AiAnomalyType.Unknown,
        RiskLevel = response.OverallRiskLevel,
        AnomalyScore = response.OverallRiskLevel switch { AiRiskLevel.Critical => 100, AiRiskLevel.High => 80, _ => 0 },
        Summary = response.OverallSummary,
        Recommendation = response.RecommendedAction.ToString(),
        Source = "HookBridge.AI.Orchestration",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
