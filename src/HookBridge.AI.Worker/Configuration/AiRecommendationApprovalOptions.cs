namespace HookBridge.AI.Worker.Configuration;

public sealed class AiRecommendationApprovalOptions
{
    public const string SectionName = "AiRecommendationApproval";

    public bool RequireApprovalForHighRisk { get; set; } = true;

    public bool RequireApprovalForCriticalRisk { get; set; } = true;

    public bool RequireApprovalForSecurityActions { get; set; } = true;

    public bool RequireApprovalForTransformations { get; set; } = true;

    public bool AllowLowRiskAutoApproval { get; set; }

    public int ApprovalExpiryHours { get; set; } = 72;
}
