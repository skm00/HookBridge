namespace HookBridge.AI.Worker.DTOs;

public sealed class HumanApprovalWorkflowResponseDto
{
    public string? ApprovalId { get; set; }
    public string RecommendationId { get; set; } = string.Empty;
    public AiRecommendationType RecommendationType { get; set; }
    public AiRecommendationApprovalStatus ApprovalStatus { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public bool RequiresApproval { get; set; }
    public bool CanApply { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public double? ConfidenceScore { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? ConfidenceExplanation { get; set; }
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
