namespace HookBridge.Domain.Entities;

/// <summary>
/// Monthly usage counters for a tenant.
/// </summary>
public sealed class UsageMetric : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public long EventsReceived { get; set; }

    public long EventsDelivered { get; set; }

    public long EventsFailed { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
