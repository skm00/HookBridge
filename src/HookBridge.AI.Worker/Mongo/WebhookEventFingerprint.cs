using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookEventFingerprint
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("eventId")] public string? EventId { get; set; }
    [BsonElement("correlationId")] public string? CorrelationId { get; set; }
    [BsonElement("customerId")] public string? CustomerId { get; set; }
    [BsonElement("customerIdType")] public string? CustomerIdType { get; set; }
    [BsonElement("subscriptionId")] public string? SubscriptionId { get; set; }
    [BsonElement("endpointId")] public string? EndpointId { get; set; }
    [BsonElement("environment")] public string? Environment { get; set; }
    [BsonElement("eventType")] public string? EventType { get; set; }
    [BsonElement("source")] public string? Source { get; set; }
    [BsonElement("targetUrl")] public string? TargetUrl { get; set; }
    [BsonElement("payloadHash")] public string? PayloadHash { get; set; }
    [BsonElement("signatureHash")] public string? SignatureHash { get; set; }
    [BsonElement("eventTimestampUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime? EventTimestampUtc { get; set; }
    [BsonElement("receivedAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ReceivedAtUtc { get; set; }
    [BsonElement("createdAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    [BsonElement("expiresAtUtc")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ExpiresAtUtc { get; set; }
}
