namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookTransformationRecommendationSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? EventType { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Limit { get; set; } = 100;
}
