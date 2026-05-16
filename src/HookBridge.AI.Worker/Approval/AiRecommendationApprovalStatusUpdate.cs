using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Approval;

public sealed class AiRecommendationApprovalStatusUpdate
{
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public string? AppliedBy { get; set; }
    public string? ApplyComment { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
}
