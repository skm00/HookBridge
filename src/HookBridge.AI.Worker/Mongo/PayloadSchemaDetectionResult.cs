using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class PayloadSchemaDetectionResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("correlationId")]
    public string? CorrelationId { get; set; }

    [BsonElement("source")]
    public string? Source { get; set; }

    [BsonElement("customerId")]
    public string? CustomerId { get; set; }

    [BsonElement("detectedSchemaName")]
    public string DetectedSchemaName { get; set; } = string.Empty;

    [BsonElement("detectedEventType")]
    public string DetectedEventType { get; set; } = string.Empty;

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("importantFields")]
    public List<PayloadFieldInsightDto> ImportantFields { get; set; } = [];

    [BsonElement("missingFields")]
    public List<string> MissingFields { get; set; } = [];

    [BsonElement("validationIssues")]
    public List<string> ValidationIssues { get; set; } = [];

    [BsonElement("suggestedDtoName")]
    public string SuggestedDtoName { get; set; } = string.Empty;

    [BsonElement("confidenceScore")]
    public double ConfidenceScore { get; set; }
    [BsonElement("confidenceLevel")]
    public string ConfidenceLevel { get; set; } = "Unknown";
    [BsonElement("confidenceExplanation")]
    public string ConfidenceExplanation { get; set; } = string.Empty;

    [BsonElement("riskLevel")]
    public string RiskLevel { get; set; } = "Unknown";

    [BsonElement("generatedAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime GeneratedAtUtc { get; set; }

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("provider")]
    public string Provider { get; set; } = string.Empty;

    [BsonElement("fallbackUsed")]
    public bool FallbackUsed { get; set; }


    [BsonElement("promptName")]
    public string PromptName { get; set; } = string.Empty;

    [BsonElement("promptVersion")]
    public string PromptVersion { get; set; } = string.Empty;

    [BsonElement("promptHash")]
    public string PromptHash { get; set; } = string.Empty;


    [BsonElement("approvalStatus")]
    [BsonRepresentation(BsonType.String)]
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; } = AiRecommendationApprovalStatus.PendingReview;

    [BsonElement("approvalId")]
    [BsonIgnoreIfNull]
    public string? ApprovalId { get; set; }

    [BsonElement("requiresApproval")]
    public bool RequiresApproval { get; set; } = true;

    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static PayloadSchemaDetectionResult FromResponse(
        PayloadSchemaDetectionResponseDto response,
        PayloadSchemaDetectionRequestDto request)
        => new()
        {
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            Source = request.Source,
            CustomerId = request.CustomerId,
            DetectedSchemaName = response.DetectedSchemaName,
            DetectedEventType = response.DetectedEventType,
            Summary = response.Summary,
            ImportantFields = response.ImportantFields.ToList(),
            MissingFields = response.MissingFields.ToList(),
            ValidationIssues = response.ValidationIssues.ToList(),
            SuggestedDtoName = response.SuggestedDtoName,
            ConfidenceScore = response.ConfidenceScore,
            ConfidenceLevel = response.ConfidenceLevel.ToString(),
            ConfidenceExplanation = response.ConfidenceExplanation,
            RiskLevel = response.RiskLevel,
            GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc),
            Model = response.Model,
            Provider = response.Provider,
            PromptName = response.PromptName,
            PromptVersion = response.PromptVersion,
            PromptHash = response.PromptHash,
            FallbackUsed = response.Fallback?.UsedFallback ?? false,
            CreatedAtUtc = DateTime.UtcNow
        };
}
