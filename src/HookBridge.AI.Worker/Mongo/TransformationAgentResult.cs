using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class TransformationAgentResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }

    [BsonRepresentation(BsonType.String)]
    public TransformationAgentDecision TransformationDecision { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<WebhookFieldMappingRecommendationDto> RecommendedMappings { get; set; } = [];
    public List<string> MissingTargetFields { get; set; } = [];
    public List<string> UnmappedSourceFields { get; set; } = [];
    public string GeneratedTransformationCode { get; set; } = string.Empty;
    [BsonRepresentation(BsonType.String)]
    public List<TransformationAgentReasonCode> ReasonCodes { get; set; } = [];
    public double ConfidenceScore { get; set; }
    [BsonElement("confidenceLevel")]
    public string ConfidenceLevel { get; set; } = "Unknown";
    [BsonElement("confidenceExplanation")]
    public string ConfidenceExplanation { get; set; } = string.Empty;
    public bool Fallback { get; set; }
    public string PromptName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime ReceivedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime GeneratedAtUtc { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static TransformationAgentResult FromResponse(TransformationAgentResponseDto response, TransformationAgentRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        CustomerId = request.CustomerId,
        CustomerIdType = request.CustomerIdType,
        SubscriptionId = request.SubscriptionId,
        EndpointId = request.EndpointId,
        Environment = request.Environment,
        EventType = request.EventType,
        Source = request.Source,
        TransformationDecision = response.TransformationDecision,
        RiskLevel = response.RiskLevel,
        RequiresApproval = response.RequiresApproval,
        Summary = response.Summary,
        Recommendation = response.Recommendation,
        RecommendedMappings = response.RecommendedMappings.ToList(),
        MissingTargetFields = response.MissingTargetFields.ToList(),
        UnmappedSourceFields = response.UnmappedSourceFields.ToList(),
        GeneratedTransformationCode = response.GeneratedTransformationCode,
        ReasonCodes = response.ReasonCodes,
        ConfidenceScore = response.ConfidenceScore,
            ConfidenceLevel = response.ConfidenceLevel.ToString(),
            ConfidenceExplanation = response.ConfidenceExplanation,
        Fallback = response.Fallback,
        PromptName = response.PromptName,
        PromptVersion = response.PromptVersion,
        PromptHash = response.PromptHash,
        ReceivedAtUtc = request.ReceivedAtUtc,
        GeneratedAtUtc = response.GeneratedAtUtc
    };
}
