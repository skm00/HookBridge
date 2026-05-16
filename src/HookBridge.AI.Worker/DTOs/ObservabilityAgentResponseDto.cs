using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class ObservabilityAgentResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? Environment { get; set; }
    public string? ServiceName { get; set; }
    public ObservabilityStatus ObservabilityStatus { get; set; } = ObservabilityStatus.Unknown;
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<ObservabilitySignalDto> Signals { get; set; } = Array.Empty<ObservabilitySignalDto>();
    public IReadOnlyList<ObservabilitySuggestedAction> SuggestedActions { get; set; } = Array.Empty<ObservabilitySuggestedAction>();
    public double ConfidenceScore { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Fallback { get; set; }
    public string PromptName { get; set; } = "observability-agent";
    public string PromptVersion { get; set; } = "v1.0.0";
    public string PromptHash { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
        if (ConfidenceScore is < 0 or > 1) yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
    }
}
