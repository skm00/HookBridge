using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class SecurityAgentResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public bool IsSuspicious { get; set; }
    public SecurityAgentDecision SecurityDecision { get; set; } = SecurityAgentDecision.None;
    public int SecurityRiskScore { get; set; }
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public IReadOnlyList<AiSecuritySignalDto> SecuritySignals { get; set; } = Array.Empty<AiSecuritySignalDto>();
    public IReadOnlyList<SecurityAgentReasonCode> ReasonCodes { get; set; } = Array.Empty<SecurityAgentReasonCode>();
    public double ConfidenceScore { get; set; }
    public AiConfidenceLevel ConfidenceLevel { get; set; } = AiConfidenceLevel.Unknown;
    public string ConfidenceExplanation { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Fallback { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
        if (SecurityRiskScore is < 0 or > 100) yield return new ValidationResult("SecurityRiskScore must be between 0 and 100.", [nameof(SecurityRiskScore)]);
        if (ConfidenceScore is < 0 or > 1) yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
    }
}
