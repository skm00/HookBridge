using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSafeModeAuditRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("actionType")]
    [BsonRepresentation(BsonType.String)]
    public AiActionType ActionType { get; set; }

    [BsonElement("decision")]
    [BsonRepresentation(BsonType.String)]
    public AiSafeModeDecision Decision { get; set; }

    [BsonElement("environment")]
    public string Environment { get; set; } = string.Empty;

    [BsonElement("eventId")]
    [BsonIgnoreIfNull]
    public string? EventId { get; set; }

    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    [BsonElement("customerId")]
    [BsonIgnoreIfNull]
    public string? CustomerId { get; set; }

    [BsonElement("subscriptionId")]
    [BsonIgnoreIfNull]
    public string? SubscriptionId { get; set; }

    [BsonElement("endpointId")]
    [BsonIgnoreIfNull]
    public string? EndpointId { get; set; }

    [BsonElement("riskLevel")]
    [BsonIgnoreIfNull]
    public string? RiskLevel { get; set; }

    [BsonElement("confidenceScore")]
    [BsonIgnoreIfNull]
    public double? ConfidenceScore { get; set; }

    [BsonElement("approvalId")]
    [BsonIgnoreIfNull]
    public string? ApprovalId { get; set; }

    [BsonElement("approvalStatus")]
    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public AiRecommendationApprovalStatus? ApprovalStatus { get; set; }

    [BsonElement("requestedBy")]
    [BsonIgnoreIfNull]
    public string? RequestedBy { get; set; }

    [BsonElement("reason")]
    [BsonIgnoreIfNull]
    public string? Reason { get; set; }

    [BsonElement("blockMessage")]
    [BsonIgnoreIfNull]
    public string? BlockMessage { get; set; }

    [BsonElement("evaluatedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
}
