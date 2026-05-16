namespace HookBridge.AI.Worker.DTOs;

public sealed class TransformationAgentSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public TransformationAgentDecision? TransformationDecision { get; set; }
    public string? RiskLevel { get; set; }
    public bool? RequiresApproval { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
}
