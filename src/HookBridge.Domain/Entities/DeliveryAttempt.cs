using HookBridge.Domain.Enums;

namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents a delivery attempt log entry for a webhook event subscription.
/// </summary>
public sealed class DeliveryAttempt : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public DeliveryStatus Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public long DurationMs { get; set; }

    public DateTime AttemptedAt { get; set; }

    public string? CorrelationId { get; set; }
}
