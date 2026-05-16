using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiRecommendationApproval
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("recommendationId")]
    public string RecommendationId { get; set; } = string.Empty;

    [BsonElement("eventId")]
    [BsonIgnoreIfNull]
    public string? EventId { get; set; }

    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    [BsonElement("customerId")]
    [BsonIgnoreIfNull]
    public string? CustomerId { get; set; }

    [BsonElement("customerIdType")]
    [BsonIgnoreIfNull]
    public string? CustomerIdType { get; set; }

    [BsonElement("subscriptionId")]
    [BsonIgnoreIfNull]
    public string? SubscriptionId { get; set; }

    [BsonElement("endpointId")]
    [BsonIgnoreIfNull]
    public string? EndpointId { get; set; }

    [BsonElement("environment")]
    [BsonIgnoreIfNull]
    public string? Environment { get; set; }

    [BsonElement("recommendationType")]
    [BsonRepresentation(BsonType.String)]
    public AiRecommendationType RecommendationType { get; set; }

    [BsonElement("approvalStatus")]
    [BsonRepresentation(BsonType.String)]
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; } = AiRecommendationApprovalStatus.PendingReview;

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [BsonElement("suggestedAction")]
    [BsonIgnoreIfNull]
    public string? SuggestedAction { get; set; }

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [BsonElement("requiresApproval")]
    public bool RequiresApproval { get; set; } = true;

    [BsonElement("requestedBy")]
    [BsonIgnoreIfNull]
    public string? RequestedBy { get; set; }

    [BsonElement("reviewedBy")]
    [BsonIgnoreIfNull]
    public string? ReviewedBy { get; set; }

    [BsonElement("reviewComment")]
    [BsonIgnoreIfNull]
    public string? ReviewComment { get; set; }

    [BsonElement("appliedBy")]
    [BsonIgnoreIfNull]
    public string? AppliedBy { get; set; }

    [BsonElement("applyComment")]
    [BsonIgnoreIfNull]
    public string? ApplyComment { get; set; }

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [BsonElement("reviewedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? ReviewedAtUtc { get; set; }

    [BsonElement("appliedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? AppliedAtUtc { get; set; }

    [BsonElement("expiresAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    [BsonIgnoreIfNull]
    public DateTime? ExpiresAtUtc { get; set; }
}
