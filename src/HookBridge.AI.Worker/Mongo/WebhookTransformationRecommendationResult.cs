using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookTransformationRecommendationResult
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
    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;
    [BsonElement("recommendedMappings")]
    public List<WebhookFieldMappingRecommendationDto> RecommendedMappings { get; set; } = [];
    [BsonElement("missingTargetFields")]
    public List<string> MissingTargetFields { get; set; } = [];
    [BsonElement("unmappedSourceFields")]
    public List<string> UnmappedSourceFields { get; set; } = [];
    [BsonElement("transformationNotes")]
    public List<string> TransformationNotes { get; set; } = [];
    [BsonElement("generatedTransformationCode")]
    public string GeneratedTransformationCode { get; set; } = string.Empty;
    [BsonElement("confidenceScore")]
    public double ConfidenceScore { get; set; }
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
    [BsonElement("createdAtUtc")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static WebhookTransformationRecommendationResult FromResponse(WebhookTransformationRecommendationResponseDto response, WebhookTransformationRecommendationRequestDto request) => new()
    {
        EventId = response.EventId,
        CorrelationId = response.CorrelationId,
        Source = request.Source,
        CustomerId = request.CustomerId,
        Summary = response.Summary,
        RecommendedMappings = response.RecommendedMappings.ToList(),
        MissingTargetFields = response.MissingTargetFields.ToList(),
        UnmappedSourceFields = response.UnmappedSourceFields.ToList(),
        TransformationNotes = response.TransformationNotes.ToList(),
        GeneratedTransformationCode = response.GeneratedTransformationCode,
        ConfidenceScore = response.ConfidenceScore,
        RiskLevel = response.RiskLevel,
        GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc),
        Model = response.Model,
        Provider = response.Provider,
        FallbackUsed = response.Fallback?.UsedFallback ?? false,
        CreatedAtUtc = DateTime.UtcNow
    };
}
