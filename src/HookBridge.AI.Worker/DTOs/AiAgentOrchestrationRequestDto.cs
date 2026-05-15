using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiAgentOrchestrationRequestDto : IValidatableObject
{
    [Required]
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
    public int? StatusCode { get; set; }
    public string? FailureReason { get; set; }
    public IDictionary<string, string>? Headers { get; set; }
    public object? Payload { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(EventId))
        {
            yield return new ValidationResult("EventId is required.", [nameof(EventId)]);
        }

        if (ReceivedAtUtc.Kind != DateTimeKind.Utc)
        {
            yield return new ValidationResult("ReceivedAtUtc must be UTC.", [nameof(ReceivedAtUtc)]);
        }

        if (!string.IsNullOrWhiteSpace(TargetUrl) && !Uri.TryCreate(TargetUrl, UriKind.Absolute, out _))
        {
            yield return new ValidationResult("TargetUrl must be a valid absolute URL when provided.", [nameof(TargetUrl)]);
        }
    }
}
