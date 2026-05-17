using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AutoRemediationRecommendationResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public AutoRemediationType RemediationType { get; set; }
    public AutoRemediationRecommendedAction RecommendedAction { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public double ConfidenceScore { get; set; }
    public bool RequiresApproval { get; set; }
    public AiSafeModeDecision SafeModeDecision { get; set; } = AiSafeModeDecision.Allowed;
    public string SafeModeReason { get; set; } = string.Empty;
    public bool IsActionAllowed { get; set; } = true;
    public bool CanAutoApply { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<string> Steps { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AutoRemediationReasonCode> ReasonCodes { get; set; } = Array.Empty<AutoRemediationReasonCode>();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConfidenceScore is < 0 or > 1) yield return new("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
    }
}
