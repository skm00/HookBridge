namespace HookBridge.Application.DTOs.FailedEvents;

public sealed class FailedEventResponseDto
{
    public string Id { get; set; } = string.Empty;

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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class FailedEventSearchRequestDto
{
    public string? TenantId { get; set; }

    public string? EventId { get; set; }

    public string? SubscriptionId { get; set; }

    public string? EventType { get; set; }

    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
