using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiAgentResultDto : IValidatableObject
{
    public AiAgentName AgentName { get; set; }
    public bool IsSuccessful { get; set; }
    public string Summary { get; set; } = string.Empty;
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public string SuggestedAction { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public bool UsedFallback { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConfidenceScore is < 0 or > 1)
        {
            yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        }
    }
}
