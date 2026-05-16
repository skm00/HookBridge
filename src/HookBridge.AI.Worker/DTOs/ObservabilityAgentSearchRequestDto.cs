namespace HookBridge.AI.Worker.DTOs;

public sealed class ObservabilityAgentSearchRequestDto
{
    public string? Environment { get; set; }
    public string? ServiceName { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public ObservabilityStatus? ObservabilityStatus { get; set; }
    public AiRiskLevel? RiskLevel { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
}
