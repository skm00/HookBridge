namespace HookBridge.Application.DTOs.Events;

public sealed class IncomingEventResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime? SourceTimestamp { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public string? ApiKeyId { get; set; }

    public string? CorrelationId { get; set; }

    public object Payload { get; set; } = new { };
}

public sealed class IncomingEventSearchRequestDto
{
    public string? TenantId { get; set; }

    public string? EventId { get; set; }

    public string? EventType { get; set; }

    public string? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? CorrelationId { get; set; }
}
