using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class DeadLetterAiAnalysisResponseDto : IValidatableObject
{
    public string DeadLetterId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DeadLetterReplaySafety ReplaySafety { get; set; }
    public DeadLetterSuggestedAction SuggestedAction { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public double ConfidenceScore { get; set; }
    public AiConfidenceLevel ConfidenceLevel { get; set; } = AiConfidenceLevel.Unknown;
    public bool RequiresApproval { get; set; }
    public AiSafeModeDecision SafeModeDecision { get; set; } = AiSafeModeDecision.RequiresApproval;
    public bool IsActionAllowed { get; set; }
    public IReadOnlyList<DeadLetterReasonCode> ReasonCodes { get; set; } = Array.Empty<DeadLetterReasonCode>();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public AiFallbackMetadataDto Fallback { get; set; } = new();
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? PromptHash { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConfidenceScore is < 0 or > 1) yield return new("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
    }
}
