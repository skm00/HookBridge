using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Approval;

public static class HumanApprovalWorkflowRules
{
    private static readonly HashSet<(AiRecommendationApprovalStatus From, AiRecommendationApprovalStatus To)> ValidTransitions = new()
    {
        (AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Approved),
        (AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Rejected),
        (AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.NeedsMoreInfo),
        (AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Approved),
        (AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Rejected),
        (AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Applied),
        (AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Expired),
        (AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Expired)
    };

    public static bool CanTransition(AiRecommendationApprovalStatus from, AiRecommendationApprovalStatus to)
        => ValidTransitions.Contains((from, to));

    public static bool CanApply(AiRecommendationApprovalStatus status)
        => status == AiRecommendationApprovalStatus.Approved;

    public static bool RequiresApproval(AiRecommendationType recommendationType, string? riskLevel, string? suggestedAction, HumanApprovalWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return false;
        }

        if (IsRisk(riskLevel, "Critical") && options.RequireApprovalForCriticalRisk) return true;
        if (IsRisk(riskLevel, "High") && options.RequireApprovalForHighRisk) return true;
        if (recommendationType == AiRecommendationType.SecurityRecommendation && options.RequireApprovalForSecurityActions) return true;
        if (recommendationType == AiRecommendationType.TransformationRecommendation && options.RequireApprovalForTransformations) return true;
        if (IsGeneratedCodeRecommendation(recommendationType, suggestedAction)) return true;

        return recommendationType == AiRecommendationType.RetryRecommendation && IsRisk(riskLevel, "Low")
            ? !options.AllowLowRiskAutoApproval
            : true;
    }

    private static bool IsRisk(string? actual, string expected)
        => string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsGeneratedCodeRecommendation(AiRecommendationType recommendationType, string? suggestedAction)
        => recommendationType is AiRecommendationType.DtoSuggestion or AiRecommendationType.ValidationRuleRecommendation
           || (suggestedAction?.Contains("generated code", StringComparison.OrdinalIgnoreCase) ?? false)
           || (suggestedAction?.Contains("generate code", StringComparison.OrdinalIgnoreCase) ?? false);
}
