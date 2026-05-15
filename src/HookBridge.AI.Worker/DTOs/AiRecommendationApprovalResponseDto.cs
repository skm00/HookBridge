namespace HookBridge.AI.Worker.DTOs;

public sealed class AiRecommendationApprovalResponseDto
{
    public string? Id { get; set; }
    public string RecommendationId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public AiRecommendationType RecommendationType { get; set; }
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
