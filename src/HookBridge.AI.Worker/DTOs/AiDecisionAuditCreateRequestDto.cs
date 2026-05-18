using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiDecisionAuditCreateRequestDto : IValidatableObject
{
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? AgentName { get; set; }
    [Required]
    public AiDecisionAuditType DecisionType { get; set; }
    public string? Decision { get; set; }
    public string? RiskLevel { get; set; }
    [Range(0.0, 1.0, ErrorMessage = "ConfidenceScore must be between 0 and 1.")]
    public double? ConfidenceScore { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? SuggestedAction { get; set; }
    public bool? RequiresApproval { get; set; }
    public string? ApprovalId { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? SafeModeDecision { get; set; }
    public bool? IsActionAllowed { get; set; }
    public bool? UsedAi { get; set; }
    public bool? UsedFallback { get; set; }
    public string? FallbackReason { get; set; }
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? PromptHash { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? Summary { get; set; }
    public string? Recommendation { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public string? CreatedBy { get; set; }
    public Dictionary<string, string?> Metadata { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DecisionType == AiDecisionAuditType.Unknown)
        {
            yield return new ValidationResult("DecisionType is required.", [nameof(DecisionType)]);
        }
    }
}
