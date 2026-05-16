using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.TransformationAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class TransformationAgentWorker : BackgroundService
{
    private readonly ITransformationAgentConsumer _consumer;
    private readonly ITransformationAgent _agent;
    private readonly ITransformationAgentResultRepository _repository;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<TransformationAgentWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public TransformationAgentWorker(ITransformationAgentConsumer consumer, ITransformationAgent agent, ITransformationAgentResultRepository repository, IAiRecommendationApprovalService approvalService, IAiAnomalyProducer anomalyProducer, ILogger<TransformationAgentWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _approvalService = approvalService;
        _anomalyProducer = anomalyProducer;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.TransformationAgentTopic))
        {
            _logger.LogInformation("Transformation agent topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["CustomerId"] = request.CustomerId });
            var response = await _agent.AnalyzeAsync(request, stoppingToken);
            await _repository.InsertAsync(TransformationAgentResult.FromResponse(response, request), stoppingToken);
            _logger.LogInformation("Result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, TransformationDecision: {TransformationDecision}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.TransformationDecision, response.RiskLevel, response.RequiresApproval);

            if (response.RequiresApproval)
            {
                await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                {
                    RecommendationId = $"transformation-agent:{response.EventId}:{response.GeneratedAtUtc:O}",
                    EventId = response.EventId,
                    CorrelationId = response.CorrelationId,
                    CustomerId = request.CustomerId,
                    SubscriptionId = request.SubscriptionId,
                    EndpointId = request.EndpointId,
                    RecommendationType = AiRecommendationType.TransformationRecommendation,
                    RiskLevel = response.RiskLevel,
                    SuggestedAction = response.TransformationDecision.ToString(),
                    Summary = response.Summary,
                    Recommendation = response.Recommendation,
                    RequestedBy = "HookBridge.AI.TransformationAgent"
                }, stoppingToken);
            }

            if (!string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic) && response.RiskLevel is "High" or "Critical")
            {
                await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
            }
        }
    }

    private static AiAnomalyEventDto ToAnomalyEvent(TransformationAgentResponseDto response, TransformationAgentRequestDto request) => new()
    {
        AnomalyId = $"transformation_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        AnomalyType = AiAnomalyType.SuspiciousPayloadSpike,
        RiskLevel = Enum.TryParse<AiRiskLevel>(response.RiskLevel, true, out var risk) ? risk : AiRiskLevel.Unknown,
        AnomalyScore = response.RiskLevel == "Critical" ? 95 : 80,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.TransformationAgent",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
