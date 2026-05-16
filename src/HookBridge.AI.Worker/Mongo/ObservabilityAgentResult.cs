using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class ObservabilityAgentResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? Environment { get; set; }
    public string? ServiceName { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public long KafkaConsumerLag { get; set; }
    public string? KafkaTopic { get; set; }
    public string? KafkaConsumerGroupId { get; set; }
    public bool MongoIsHealthy { get; set; }
    public long MongoLatencyMs { get; set; }
    public long TotalDeliveries { get; set; }
    public long FailedDeliveries { get; set; }
    public long RetryCount { get; set; }
    public long DeadLetterCount { get; set; }
    public int AnomalyCount { get; set; }
    public int SecurityFindingCount { get; set; }
    public int ErrorLogCount { get; set; }
    public int WarningLogCount { get; set; }
    public ObservabilityStatus ObservabilityStatus { get; set; }
    public AiRiskLevel RiskLevel { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<ObservabilitySignalDto> Signals { get; set; } = [];
    public List<ObservabilitySuggestedAction> SuggestedActions { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTime EvaluationWindowFromUtc { get; set; }
    public DateTime EvaluationWindowToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public bool Fallback { get; set; }
    public string PromptName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;

    public static ObservabilityAgentResult FromResponse(ObservabilityAgentResponseDto response, ObservabilityAgentRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        Environment = response.Environment,
        ServiceName = response.ServiceName,
        CustomerId = request.CustomerId,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        KafkaConsumerLag = request.KafkaConsumerLag,
        KafkaTopic = request.KafkaTopic,
        KafkaConsumerGroupId = request.KafkaConsumerGroupId,
        MongoIsHealthy = request.MongoIsHealthy,
        MongoLatencyMs = request.MongoLatencyMs,
        TotalDeliveries = request.TotalDeliveries,
        FailedDeliveries = request.FailedDeliveries,
        RetryCount = request.RetryCount,
        DeadLetterCount = request.DeadLetterCount,
        AnomalyCount = request.AnomalyCount,
        SecurityFindingCount = request.SecurityFindingCount,
        ErrorLogCount = request.ErrorLogCount,
        WarningLogCount = request.WarningLogCount,
        ObservabilityStatus = response.ObservabilityStatus,
        RiskLevel = response.RiskLevel,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Signals = response.Signals.ToList(),
        SuggestedActions = response.SuggestedActions.ToList(),
        ConfidenceScore = response.ConfidenceScore,
        RequiresApproval = response.RequiresApproval,
        EvaluationWindowFromUtc = request.EvaluationWindowFromUtc,
        EvaluationWindowToUtc = request.EvaluationWindowToUtc,
        CreatedAtUtc = request.CreatedAtUtc,
        GeneratedAtUtc = response.GeneratedAtUtc,
        Fallback = response.Fallback,
        PromptName = response.PromptName,
        PromptVersion = response.PromptVersion,
        PromptHash = response.PromptHash
    };
}
