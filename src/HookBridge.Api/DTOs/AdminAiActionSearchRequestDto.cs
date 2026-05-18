using HookBridge.AI.Worker.DTOs;

namespace HookBridge.Api.DTOs;

public sealed class AdminAiActionSearchRequestDto
{
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public AiRecommendationType? RecommendationType { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
