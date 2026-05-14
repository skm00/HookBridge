namespace HookBridge.AI.Worker.DTOs;

public sealed class AiAnomalyEventDto
{
    public string AnomalyId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? TargetUrl { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public AiAnomalyType AnomalyType { get; set; } = AiAnomalyType.Unknown;
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public int AnomalyScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Source { get; set; } = "HookBridge.AI.Worker";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
