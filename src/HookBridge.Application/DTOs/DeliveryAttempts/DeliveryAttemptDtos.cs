using HookBridge.Domain.Enums;

namespace HookBridge.Application.DTOs.DeliveryAttempts;

public sealed class DeliveryAttemptResponseDto
{
    public string Id { get; set; } = string.Empty;

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

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class DeliveryAttemptSearchRequestDto
{
    public string? TenantId { get; set; }

    public string? EventId { get; set; }

    public string? SubscriptionId { get; set; }

    public string? EventType { get; set; }

    public DeliveryStatus? Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? TargetUrl { get; set; }
}
