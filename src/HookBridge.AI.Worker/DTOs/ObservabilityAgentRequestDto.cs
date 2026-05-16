using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class ObservabilityAgentRequestDto : IValidatableObject
{
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
    public bool MongoIsHealthy { get; set; } = true;
    public long MongoLatencyMs { get; set; }
    public long TotalDeliveries { get; set; }
    public long FailedDeliveries { get; set; }
    public long RetryCount { get; set; }
    public long DeadLetterCount { get; set; }
    public int AnomalyCount { get; set; }
    public int SecurityFindingCount { get; set; }
    public int ErrorLogCount { get; set; }
    public int WarningLogCount { get; set; }
    public IReadOnlyList<ObservabilityLogEntryDto> RecentErrors { get; set; } = Array.Empty<ObservabilityLogEntryDto>();
    public DateTime EvaluationWindowFromUtc { get; set; } = DateTime.UtcNow.AddMinutes(-15);
    public DateTime EvaluationWindowToUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(EventId)) yield return new ValidationResult("EventId is required.", [nameof(EventId)]);
        if (EvaluationWindowFromUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("EvaluationWindowFromUtc must be UTC.", [nameof(EvaluationWindowFromUtc)]);
        if (EvaluationWindowToUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("EvaluationWindowToUtc must be UTC.", [nameof(EvaluationWindowToUtc)]);
        if (CreatedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("CreatedAtUtc must be UTC.", [nameof(CreatedAtUtc)]);
        if (EvaluationWindowToUtc <= EvaluationWindowFromUtc) yield return new ValidationResult("EvaluationWindowToUtc must be greater than EvaluationWindowFromUtc.", [nameof(EvaluationWindowToUtc)]);
        if (KafkaConsumerLag < 0) yield return new ValidationResult("KafkaConsumerLag must be greater than or equal to 0.", [nameof(KafkaConsumerLag)]);
        if (MongoLatencyMs < 0) yield return new ValidationResult("MongoLatencyMs must be greater than or equal to 0.", [nameof(MongoLatencyMs)]);
        if (TotalDeliveries < 0) yield return new ValidationResult("TotalDeliveries must be greater than or equal to 0.", [nameof(TotalDeliveries)]);
        if (FailedDeliveries < 0) yield return new ValidationResult("FailedDeliveries must be greater than or equal to 0.", [nameof(FailedDeliveries)]);
        if (RetryCount < 0) yield return new ValidationResult("RetryCount must be greater than or equal to 0.", [nameof(RetryCount)]);
        if (DeadLetterCount < 0) yield return new ValidationResult("DeadLetterCount must be greater than or equal to 0.", [nameof(DeadLetterCount)]);
        if (AnomalyCount < 0) yield return new ValidationResult("AnomalyCount must be greater than or equal to 0.", [nameof(AnomalyCount)]);
        if (SecurityFindingCount < 0) yield return new ValidationResult("SecurityFindingCount must be greater than or equal to 0.", [nameof(SecurityFindingCount)]);
        if (ErrorLogCount < 0) yield return new ValidationResult("ErrorLogCount must be greater than or equal to 0.", [nameof(ErrorLogCount)]);
        if (WarningLogCount < 0) yield return new ValidationResult("WarningLogCount must be greater than or equal to 0.", [nameof(WarningLogCount)]);
        if (FailedDeliveries > TotalDeliveries) yield return new ValidationResult("FailedDeliveries cannot exceed TotalDeliveries.", [nameof(FailedDeliveries)]);
        foreach (var error in RecentErrors ?? Array.Empty<ObservabilityLogEntryDto>())
        {
            if (error.TimestampUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("RecentErrors timestamps must be UTC.", [nameof(RecentErrors)]);
        }
    }
}
