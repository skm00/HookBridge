namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Kafka message payload for AI analysis requests and events.
/// </summary>
public sealed class AiAnalysisEventDto
{
    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string Source { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
