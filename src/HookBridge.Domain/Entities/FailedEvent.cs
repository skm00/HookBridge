namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents an event that permanently failed delivery and was moved to the dead letter queue.
/// </summary>
public sealed class FailedEvent : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public int FinalAttemptNumber { get; set; }

    public int? LastHttpStatusCode { get; set; }

    public string? LastErrorMessage { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime FailedAt { get; set; }

    public string? CorrelationId { get; set; }
}
