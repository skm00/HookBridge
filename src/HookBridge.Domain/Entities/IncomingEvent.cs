namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents a tenant event accepted for downstream delivery processing.
/// </summary>
public sealed class IncomingEvent : BaseEntity
{
    public string IdempotencyId => EventId;
    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public DateTime? SourceTimestamp { get; set; }

    public object Payload { get; set; } = new { };
    public string RawBody { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string? Source { get; set; }
    public string? SpecVersion { get; set; }
    public string ContentMode { get; set; } = "Raw";

    public string Status { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public string? ApiKeyId { get; set; }

    public string? CorrelationId { get; set; }
}
