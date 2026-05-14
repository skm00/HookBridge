namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookDuplicateReplayDetectionRequestDto
{
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? TargetUrl { get; set; }
    public IDictionary<string, string>? Headers { get; set; }
    public object? Payload { get; set; }
    public string? Signature { get; set; }
    public DateTime? EventTimestampUtc { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
