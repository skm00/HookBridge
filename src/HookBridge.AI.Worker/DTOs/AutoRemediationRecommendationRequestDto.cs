using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AutoRemediationRecommendationRequestDto : IValidatableObject
{
    [Required] public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? Source { get; set; }
    public string? EventType { get; set; }
    public string? RiskLevel { get; set; }
    public double ConfidenceScore { get; set; } = 1;
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(EventId)) yield return new("EventId is required.", [nameof(EventId)]);
        if (ConfidenceScore is < 0 or > 1) yield return new("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        if (CreatedAtUtc.Kind != DateTimeKind.Utc) yield return new("CreatedAtUtc must be UTC.", [nameof(CreatedAtUtc)]);
        if (StatusCode is < 100 or > 599) yield return new("StatusCode must be between 100 and 599 when provided.", [nameof(StatusCode)]);
        if (RetryCount < 0) yield return new("RetryCount must be greater than or equal to 0.", [nameof(RetryCount)]);
        if (MaxRetryCount < 0) yield return new("MaxRetryCount must be greater than or equal to 0.", [nameof(MaxRetryCount)]);
        if (DeadLetterCount < 0) yield return new("DeadLetterCount must be greater than or equal to 0.", [nameof(DeadLetterCount)]);
        if (KafkaConsumerLag < 0) yield return new("KafkaConsumerLag must be greater than or equal to 0.", [nameof(KafkaConsumerLag)]);
        if (MongoLatencyMs < 0) yield return new("MongoLatencyMs must be greater than or equal to 0.", [nameof(MongoLatencyMs)]);
    }
}
