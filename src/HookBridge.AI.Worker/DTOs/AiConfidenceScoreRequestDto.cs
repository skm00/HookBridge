using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiConfidenceScoreRequestDto : IValidatableObject
{
    public AiDecisionType DecisionType { get; set; } = AiDecisionType.Unknown;
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public bool UsedFallback { get; set; }
    public bool UsedAi { get; set; }
    public bool IsRuleBased { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public int EvidenceCount { get; set; }
    public int MissingDataCount { get; set; }
    public int ValidationIssueCount { get; set; }
    public int FailedAgentCount { get; set; }
    public int TotalAgentCount { get; set; }
    public bool LlmResponseWasValidJson { get; set; }
    public bool LlmResponseHadRequiredFields { get; set; }
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedAtUtc.Kind != DateTimeKind.Utc)
        {
            yield return new ValidationResult("CreatedAtUtc must be UTC.", [nameof(CreatedAtUtc)]);
        }
    }
}
