using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

/// <summary>
/// MongoDB document containing the AI-generated analysis result for a webhook event.
/// </summary>
public sealed class AiAnalysisResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("failureReason")]
    [BsonIgnoreIfNull]
    public string? FailureReason { get; set; }

    [BsonElement("aiSummary")]
    public string AiSummary { get; set; } = string.Empty;

    [BsonElement("rootCause")]
    public string RootCause { get; set; } = string.Empty;

    [BsonElement("aiRecommendation")]
    public string AiRecommendation { get; set; } = string.Empty;

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [BsonElement("confidenceScore")]
    public double ConfidenceScore { get; set; }

    [BsonElement("suggestedRetryAction")]
    public string SuggestedRetryAction { get; set; } = string.Empty;

    [BsonElement("isRetryRecommended")]
    public bool IsRetryRecommended { get; set; }

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
