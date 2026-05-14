using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

/// <summary>
/// MongoDB document containing a detected AI anomaly event for query and audit workflows.
/// </summary>
public sealed class AiAnomalyRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("anomalyId")]
    public string AnomalyId { get; set; } = string.Empty;

    [BsonElement("eventId")]
    [BsonIgnoreIfNull]
    public string? EventId { get; set; }

    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    [BsonElement("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [BsonElement("customerIdType")]
    [BsonIgnoreIfNull]
    public string? CustomerIdType { get; set; }

    [BsonElement("subscriptionId")]
    [BsonIgnoreIfNull]
    public string? SubscriptionId { get; set; }

    [BsonElement("endpointId")]
    [BsonIgnoreIfNull]
    public string? EndpointId { get; set; }

    [BsonElement("targetUrl")]
    [BsonIgnoreIfNull]
    public string? TargetUrl { get; set; }

    [BsonElement("environment")]
    [BsonIgnoreIfNull]
    public string? Environment { get; set; }

    [BsonElement("eventType")]
    [BsonIgnoreIfNull]
    public string? EventType { get; set; }

    [BsonElement("anomalyType")]
    public string AnomalyType { get; set; } = string.Empty;

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [BsonElement("anomalyScore")]
    public int AnomalyScore { get; set; }

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [BsonElement("storedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime StoredAtUtc { get; set; } = DateTime.UtcNow;
}
