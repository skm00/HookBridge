using HookBridge.AI.Worker.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HookBridge.AI.Worker.Mongo;

public sealed class FluentValidationRuleGenerationResult
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

    [BsonElement("validatorClassName")]
    public string ValidatorClassName { get; set; } = string.Empty;

    [BsonElement("namespace")]
    public string? Namespace { get; set; }

    [BsonElement("generatedValidatorCode")]
    public string GeneratedValidatorCode { get; set; } = string.Empty;

    [BsonElement("rules")]
    public List<SuggestedValidationRuleDto> Rules { get; set; } = [];

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("validationNotes")]
    public List<string> ValidationNotes { get; set; } = [];

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

    public static FluentValidationRuleGenerationResult FromResponse(FluentValidationRuleGenerationResponseDto response, FluentValidationRuleGenerationRequestDto request)
        => new()
        {
            EventId = response.EventId,
            CorrelationId = response.CorrelationId,
            Source = request.Source,
            CustomerId = request.CustomerId,
            ValidatorClassName = response.ValidatorClassName,
            Namespace = response.Namespace,
            GeneratedValidatorCode = response.GeneratedValidatorCode,
            Rules = response.Rules.ToList(),
            Summary = response.Summary,
            ValidationNotes = response.ValidationNotes.ToList(),
            ConfidenceScore = response.ConfidenceScore,
            RiskLevel = response.RiskLevel,
            GeneratedAtUtc = DateTime.SpecifyKind(response.GeneratedAtUtc, DateTimeKind.Utc),
            Model = response.Model,
            Provider = response.Provider,
            FallbackUsed = response.Fallback?.UsedFallback ?? false,
            CreatedAtUtc = DateTime.UtcNow
        };
}
