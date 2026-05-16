namespace HookBridge.AI.Worker.DTOs;

public sealed class HumanApprovalWorkflowReviewRequestDto
{
    public AiRecommendationApprovalStatus? ApprovalStatus { get; set; }
    public string ReviewedBy { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }
}
