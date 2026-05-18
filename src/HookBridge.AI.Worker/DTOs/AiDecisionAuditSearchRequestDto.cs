namespace HookBridge.AI.Worker.DTOs;

public sealed class AiDecisionAuditSearchRequestDto
{
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? AgentName { get; set; }
    public AiDecisionAuditType? DecisionType { get; set; }
    public string? RiskLevel { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? SuggestedAction { get; set; }
    public bool? RequiresApproval { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? SafeModeDecision { get; set; }
    public bool? IsActionAllowed { get; set; }
    public bool? UsedFallback { get; set; }
    public string? FallbackReason { get; set; }
    public string? PromptName { get; set; }
    public string? PromptVersion { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
