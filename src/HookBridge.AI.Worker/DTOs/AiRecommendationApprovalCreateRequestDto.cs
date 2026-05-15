namespace HookBridge.AI.Worker.DTOs;

public sealed class AiRecommendationApprovalCreateRequestDto
{
    public string RecommendationId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public AiRecommendationType? RecommendationType { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}
