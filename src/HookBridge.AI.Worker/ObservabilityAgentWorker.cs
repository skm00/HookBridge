using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.ObservabilityAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class ObservabilityAgentWorker : BackgroundService
{
    private readonly IObservabilityAgentConsumer _consumer;
    private readonly IObservabilityAgent _agent;
    private readonly IObservabilityAgentResultRepository _repository;
    private readonly IAiRecommendationApprovalService _approvalService;
    private readonly IAiAnomalyProducer _anomalyProducer;
    private readonly ILogger<ObservabilityAgentWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public ObservabilityAgentWorker(IObservabilityAgentConsumer consumer, IObservabilityAgent agent, IObservabilityAgentResultRepository repository, IAiRecommendationApprovalService approvalService, IAiAnomalyProducer anomalyProducer, ILogger<ObservabilityAgentWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
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
        if (string.IsNullOrWhiteSpace(_kafkaOptions.ObservabilityAgentTopic))
        {
            _logger.LogInformation("Observability agent topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId, ["Environment"] = request.Environment, ["ServiceName"] = request.ServiceName });
            ObservabilityAgentResponseDto response;
            try
            {
                response = await _agent.AnalyzeAsync(request, stoppingToken);
            }
            catch (Exception ex) when (ex is ValidationException or ArgumentException)
            {
                _logger.LogWarning(ex, "Invalid request. EventId: {EventId}, CorrelationId: {CorrelationId}", request.EventId, request.CorrelationId);
                continue;
            }

            await _repository.InsertAsync(ObservabilityAgentResult.FromResponse(response, request), stoppingToken);
            _logger.LogInformation("Result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, ObservabilityStatus: {ObservabilityStatus}, RiskLevel: {RiskLevel}, RequiresApproval: {RequiresApproval}", response.EventId, response.CorrelationId, response.ObservabilityStatus, response.RiskLevel, response.RequiresApproval);

            if (response.RequiresApproval)
            {
                await _approvalService.CreateAsync(new AiRecommendationApprovalCreateRequestDto
                {
                    RecommendationId = $"observability-agent:{response.EventId}:{response.GeneratedAtUtc:O}",
                    EventId = response.EventId,
                    CorrelationId = response.CorrelationId,
                    CustomerId = request.CustomerId,
                    SubscriptionId = request.SubscriptionId,
                    EndpointId = request.EndpointId,
                    RecommendationType = AiRecommendationType.AnomalyRecommendation,
                    RiskLevel = response.RiskLevel.ToString(),
                    SuggestedAction = string.Join(",", response.SuggestedActions),
                    Summary = response.Summary,
                    Recommendation = response.Recommendation,
                    RequestedBy = "HookBridge.AI.ObservabilityAgent"
                }, stoppingToken);
                _logger.LogInformation("Approval required. EventId: {EventId}, CorrelationId: {CorrelationId}, ObservabilityStatus: {ObservabilityStatus}", response.EventId, response.CorrelationId, response.ObservabilityStatus);
            }

            if (!string.IsNullOrWhiteSpace(_kafkaOptions.AnomaliesTopic) && HookBridge.AI.Worker.Services.ObservabilityAgent.ObservabilityAgent.ShouldPublishAnomaly(response))
            {
                await _anomalyProducer.PublishAsync(ToAnomalyEvent(response, request), stoppingToken);
                _logger.LogInformation("Anomaly published. EventId: {EventId}, CorrelationId: {CorrelationId}, ObservabilityStatus: {ObservabilityStatus}", response.EventId, response.CorrelationId, response.ObservabilityStatus);
            }
        }
    }

    private static AiAnomalyEventDto ToAnomalyEvent(ObservabilityAgentResponseDto response, ObservabilityAgentRequestDto request) => new()
    {
        AnomalyId = $"observability_{(string.IsNullOrWhiteSpace(response.CorrelationId) ? response.EventId : response.CorrelationId)}",
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId ?? string.Empty,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = "OperationalTelemetry",
        AnomalyType = AiAnomalyType.FailureSpike,
        RiskLevel = response.RiskLevel,
        AnomalyScore = response.ObservabilityStatus == ObservabilityStatus.Critical ? 95 : 80,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Source = "HookBridge.AI.ObservabilityAgent",
        CreatedAtUtc = response.GeneratedAtUtc
    };
}
