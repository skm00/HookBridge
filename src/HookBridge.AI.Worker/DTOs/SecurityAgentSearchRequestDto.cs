namespace HookBridge.AI.Worker.DTOs;

public sealed class SecurityAgentSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public SecurityAgentDecision? SecurityDecision { get; set; }
    public AiRiskLevel? RiskLevel { get; set; }
    public bool? IsSuspicious { get; set; }
    public bool? RequiresApproval { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
}
