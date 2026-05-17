using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AutoRemediationRecommendationResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? Source { get; set; }
    public string? EventType { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public double ConfidenceScore { get; set; }
    public string? FailureReason { get; set; }
    public int? StatusCode { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public int DeadLetterCount { get; set; }
    public long KafkaConsumerLag { get; set; }
    public bool? MongoIsHealthy { get; set; }
    public long MongoLatencyMs { get; set; }
    public bool IsSuspicious { get; set; }
    public bool IsReplay { get; set; }
    public bool IsDuplicate { get; set; }
    public string? EndpointHealthStatus { get; set; }
    public string? ObservabilityStatus { get; set; }
    public string? SecurityDecision { get; set; }
    public string? RetryDecision { get; set; }

    [BsonRepresentation(BsonType.String)] public AutoRemediationType RemediationType { get; set; }
    [BsonRepresentation(BsonType.String)] public AutoRemediationRecommendedAction RecommendedAction { get; set; }
    public bool RequiresApproval { get; set; }
    public bool CanAutoApply { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
    [BsonRepresentation(BsonType.String)] public List<AutoRemediationReasonCode> ReasonCodes { get; set; } = [];
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }

    public static AutoRemediationRecommendationResult FromResponse(AutoRemediationRecommendationResponseDto response, AutoRemediationRecommendationRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        Source = request.Source,
        EventType = request.EventType,
        RiskLevel = response.RiskLevel,
        ConfidenceScore = response.ConfidenceScore,
        FailureReason = request.FailureReason,
        StatusCode = request.StatusCode,
        RetryCount = request.RetryCount,
        MaxRetryCount = request.MaxRetryCount,
        DeadLetterCount = request.DeadLetterCount,
        KafkaConsumerLag = request.KafkaConsumerLag,
        MongoIsHealthy = request.MongoIsHealthy,
        MongoLatencyMs = request.MongoLatencyMs,
        IsSuspicious = request.IsSuspicious,
        IsReplay = request.IsReplay,
        IsDuplicate = request.IsDuplicate,
        EndpointHealthStatus = request.EndpointHealthStatus,
        ObservabilityStatus = request.ObservabilityStatus,
        SecurityDecision = request.SecurityDecision,
        RetryDecision = request.RetryDecision,
        RemediationType = response.RemediationType,
        RecommendedAction = response.RecommendedAction,
        RequiresApproval = response.RequiresApproval,
        CanAutoApply = response.CanAutoApply,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        Steps = response.Steps.ToList(),
        ReasonCodes = response.ReasonCodes.ToList(),
        CreatedAtUtc = DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Utc),
        GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc)
    };

    public AutoRemediationRecommendationResponseDto ToResponseDto() => new()
    {
        EventId = EventId,
        CorrelationId = CorrelationId,
        RemediationType = RemediationType,
        RecommendedAction = RecommendedAction,
        RiskLevel = RiskLevel,
        ConfidenceScore = ConfidenceScore,
        RequiresApproval = RequiresApproval,
        CanAutoApply = CanAutoApply,
        Summary = Summary,
        Recommendation = Recommendation,
        Steps = Steps,
        ReasonCodes = ReasonCodes,
        GeneratedAtUtc = GeneratedAtUtc
    };
}
