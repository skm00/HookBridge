using MongoDB.Bson;

namespace HookBridge.Worker.KafkaSwapBuffer;

/// <summary>
/// Webhook event payload consumed from Kafka for high-throughput persistence scenarios.
/// </summary>
public sealed class WebhookEvent
{
    /// <summary>
    /// Gets or sets the globally unique webhook event identifier. This value is indexed uniquely in MongoDB
    /// so Kafka replay after a commit failure cannot create duplicate audit, delivery, retry, DLQ, or
    /// observability records.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public string? CorrelationId { get; set; }

    public string? Source { get; set; }

    public string? Destination { get; set; }

    public int AttemptNumber { get; set; }

    public string? Status { get; set; }

    public BsonValue? Payload { get; set; }

    public BsonValue? Metadata { get; set; }
}
