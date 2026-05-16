namespace HookBridge.AI.Worker.DTOs;

public sealed class HumanApprovalWorkflowCreateRequestDto
{
    public string RecommendationId { get; set; } = string.Empty;
    public AiRecommendationType? RecommendationType { get; set; }
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public double? ConfidenceScore { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? ConfidenceExplanation { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
