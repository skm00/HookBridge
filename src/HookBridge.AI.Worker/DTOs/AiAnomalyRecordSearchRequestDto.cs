namespace HookBridge.AI.Worker.DTOs;

public sealed class AiAnomalyRecordSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string? EventType { get; set; }
    public AiAnomalyType? AnomalyType { get; set; }
    public AiRiskLevel? RiskLevel { get; set; }
    public int? MinAnomalyScore { get; set; }
    public int? MaxAnomalyScore { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
