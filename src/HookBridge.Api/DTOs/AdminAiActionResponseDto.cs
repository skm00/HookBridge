using HookBridge.AI.Worker.DTOs;

namespace HookBridge.Api.DTOs;

public sealed class AdminAiActionResponseDto
{
    public string? ApprovalId { get; set; }
    public string RecommendationId { get; set; } = string.Empty;
    public AiRecommendationType RecommendationType { get; set; }
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public bool RequiresApproval { get; set; }
    public bool CanApply { get; set; }
    public AiSafeModeDecision? SafeModeDecision { get; set; }
    public bool? IsActionAllowed { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewComment { get; set; }
    public string? AppliedBy { get; set; }
    public string? ApplyComment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
