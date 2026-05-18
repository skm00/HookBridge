using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class DeadLetterAiAnalysisRequestDto : IValidatableObject
{
    public string DeadLetterId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? TargetUrl { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string? FailureReason { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public DateTime? LastRetryAtUtc { get; set; }
    public DateTime FailedAtUtc { get; set; }
    public DateTime MovedToDeadLetterAtUtc { get; set; }
    public IDictionary<string, string>? Headers { get; set; }
    public object? Payload { get; set; }
    public string? ResponseBody { get; set; }
    public bool IsSuspicious { get; set; }
    public bool IsReplay { get; set; }
    public bool IsDuplicate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(DeadLetterId)) yield return new("DeadLetterId is required.", [nameof(DeadLetterId)]);
        if (string.IsNullOrWhiteSpace(EventId)) yield return new("EventId is required.", [nameof(EventId)]);
        if (RetryCount < 0) yield return new("RetryCount must be greater than or equal to 0.", [nameof(RetryCount)]);
        if (MaxRetryCount < 0) yield return new("MaxRetryCount must be greater than or equal to 0.", [nameof(MaxRetryCount)]);
        if (StatusCode is < 100 or > 599) yield return new("StatusCode must be between 100 and 599 when provided.", [nameof(StatusCode)]);
        if (FailedAtUtc != default && FailedAtUtc.Kind != DateTimeKind.Utc) yield return new("FailedAtUtc must be UTC.", [nameof(FailedAtUtc)]);
        if (MovedToDeadLetterAtUtc != default && MovedToDeadLetterAtUtc.Kind != DateTimeKind.Utc) yield return new("MovedToDeadLetterAtUtc must be UTC.", [nameof(MovedToDeadLetterAtUtc)]);
        if (LastRetryAtUtc is { Kind: not DateTimeKind.Utc }) yield return new("LastRetryAtUtc must be UTC when provided.", [nameof(LastRetryAtUtc)]);
        if (!string.IsNullOrWhiteSpace(TargetUrl) && !Uri.TryCreate(TargetUrl, UriKind.Absolute, out _)) yield return new("TargetUrl must be a valid URL when provided.", [nameof(TargetUrl)]);
    }
}
