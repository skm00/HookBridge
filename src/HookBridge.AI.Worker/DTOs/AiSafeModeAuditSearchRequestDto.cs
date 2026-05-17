namespace HookBridge.AI.Worker.DTOs;

public sealed class AiSafeModeAuditSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public AiActionType? ActionType { get; set; }
    public AiSafeModeDecision? Decision { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
}
