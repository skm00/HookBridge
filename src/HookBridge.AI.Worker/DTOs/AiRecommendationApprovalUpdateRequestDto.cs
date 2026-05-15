namespace HookBridge.AI.Worker.DTOs;

public sealed class AiRecommendationApprovalUpdateRequestDto
{
    public AiRecommendationApprovalStatus? ApprovalStatus { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
}
