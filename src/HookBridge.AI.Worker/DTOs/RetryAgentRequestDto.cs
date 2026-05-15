using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class RetryAgentRequestDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? TargetUrl { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string? FailureReason { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public DateTime? LastRetryAtUtc { get; set; }
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public double? EndpointRiskScore { get; set; }
    public string? EndpointRiskLevel { get; set; }
    public string? EndpointHealthStatus { get; set; }
    public long? PayloadSizeBytes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(EventId)) yield return new ValidationResult("EventId is required.", [nameof(EventId)]);
        if (RetryCount < 0) yield return new ValidationResult("RetryCount must be greater than or equal to 0.", [nameof(RetryCount)]);
        if (MaxRetryCount < 0) yield return new ValidationResult("MaxRetryCount must be greater than or equal to 0.", [nameof(MaxRetryCount)]);
        if (StatusCode is < 100 or > 599) yield return new ValidationResult("StatusCode must be between 100 and 599.", [nameof(StatusCode)]);
        if (FailedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("FailedAtUtc must be UTC.", [nameof(FailedAtUtc)]);
        if (LastRetryAtUtc.HasValue && LastRetryAtUtc.Value.Kind != DateTimeKind.Utc) yield return new ValidationResult("LastRetryAtUtc must be UTC.", [nameof(LastRetryAtUtc)]);
        if (!string.IsNullOrWhiteSpace(TargetUrl) && !Uri.TryCreate(TargetUrl, UriKind.Absolute, out _)) yield return new ValidationResult("TargetUrl must be a valid URL when provided.", [nameof(TargetUrl)]);
    }
}
