using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class RetryAgentResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public RetryAgentDecision RetryDecision { get; set; }
    public int RetryDelaySeconds { get; set; }
    public int MaxAllowedRetries { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool RequiresApproval { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<RetryAgentReasonCode> ReasonCodes { get; set; } = [];
    public double ConfidenceScore { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public bool Fallback { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GeneratedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
        if (ConfidenceScore is < 0 or > 1) yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
    }
}
