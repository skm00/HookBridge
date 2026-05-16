using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiAgentOrchestrationResponseDto : IValidatableObject
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string OverallSummary { get; set; } = string.Empty;
    public AiRiskLevel OverallRiskLevel { get; set; } = AiRiskLevel.Unknown;
    public AiOrchestrationRecommendedAction RecommendedAction { get; set; } = AiOrchestrationRecommendedAction.None;
    public double ConfidenceScore { get; set; }
    public AiConfidenceLevel ConfidenceLevel { get; set; } = AiConfidenceLevel.Unknown;
    public string ConfidenceExplanation { get; set; } = string.Empty;
    public IReadOnlyList<AiAgentResultDto> AgentResults { get; set; } = Array.Empty<AiAgentResultDto>();
    public bool RequiresApproval { get; set; }
    public string? ApprovalId { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConfidenceScore is < 0 or > 1)
        {
            yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        }

        if (GeneratedAtUtc.Kind != DateTimeKind.Utc)
        {
            yield return new ValidationResult("GeneratedAtUtc must be UTC.", [nameof(GeneratedAtUtc)]);
        }
    }
}
