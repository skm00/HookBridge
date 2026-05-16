using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class TransformationAgentResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public TransformationAgentDecision TransformationDecision { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<WebhookFieldMappingRecommendationDto> RecommendedMappings { get; set; } = Array.Empty<WebhookFieldMappingRecommendationDto>();
    public IReadOnlyList<string> MissingTargetFields { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UnmappedSourceFields { get; set; } = Array.Empty<string>();
    public string GeneratedTransformationCode { get; set; } = string.Empty;
    public List<TransformationAgentReasonCode> ReasonCodes { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Fallback { get; set; }
    public string PromptName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
        if (ConfidenceScore is < 0 or > 1) yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
    }
}
