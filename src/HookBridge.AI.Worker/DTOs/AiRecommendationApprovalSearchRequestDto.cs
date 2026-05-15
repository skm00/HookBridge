namespace HookBridge.AI.Worker.DTOs;

public sealed class AiRecommendationApprovalSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public AiRecommendationType? RecommendationType { get; set; }
    public AiRecommendationApprovalStatus? ApprovalStatus { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
