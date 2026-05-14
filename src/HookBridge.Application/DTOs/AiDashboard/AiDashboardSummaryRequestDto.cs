namespace HookBridge.Application.DTOs.AiDashboard;

/// <summary>
/// Query parameters used to filter AI dashboard summary metrics.
/// </summary>
public sealed class AiDashboardSummaryRequestDto
{
    /// <summary>Deployment environment to filter by, such as qa or production.</summary>
    public string? Environment { get; set; }

    /// <summary>Customer identifier to filter by.</summary>
    public string? CustomerId { get; set; }

    /// <summary>Customer identifier type to filter by.</summary>
    public string? CustomerIdType { get; set; }

    /// <summary>Subscription identifier to filter by.</summary>
    public string? SubscriptionId { get; set; }

    /// <summary>Endpoint identifier to filter by.</summary>
    public string? EndpointId { get; set; }

    /// <summary>Webhook event type to filter by.</summary>
    public string? EventType { get; set; }

    /// <summary>Inclusive UTC start of the dashboard date range.</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Exclusive UTC end of the dashboard date range.</summary>
    public DateTime? ToUtc { get; set; }
}
