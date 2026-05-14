namespace HookBridge.AI.Worker.Mongo;

public sealed class AiDashboardRecentFindingResult
{
    public string? Id { get; set; }
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string FindingType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
