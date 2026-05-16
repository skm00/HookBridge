using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Approval;

public static class AiRecommendationApprovalMapper
{
    public static AiRecommendationApproval ToEntity(
        AiRecommendationApprovalCreateRequestDto request,
        AiRecommendationApprovalOptions options,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        var requiresApproval = AiRecommendationApprovalRules.RequiresApproval(
            request.RecommendationType!.Value,
            request.RiskLevel,
            options);

        return new AiRecommendationApproval
        {
            RecommendationId = request.RecommendationId.Trim(),
            EventId = TrimToNull(request.EventId),
            CorrelationId = TrimToNull(request.CorrelationId),
            CustomerId = TrimToNull(request.CustomerId),
            SubscriptionId = TrimToNull(request.SubscriptionId),
            EndpointId = TrimToNull(request.EndpointId),
            RecommendationType = request.RecommendationType!.Value,
            ApprovalStatus = requiresApproval ? AiRecommendationApprovalStatus.PendingReview : AiRecommendationApprovalStatus.Approved,
            RiskLevel = request.RiskLevel.Trim(),
            SuggestedAction = TrimToNull(request.SuggestedAction),
            Summary = request.Summary?.Trim() ?? string.Empty,
            Recommendation = request.Recommendation?.Trim() ?? string.Empty,
            RequestedBy = TrimToNull(request.RequestedBy),
            RequiresApproval = requiresApproval,
            CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(createdAtUtc.AddHours(options.ApprovalExpiryHours), DateTimeKind.Utc)
        };
    }

    public static AiRecommendationApprovalResponseDto ToResponseDto(AiRecommendationApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);

        return new AiRecommendationApprovalResponseDto
        {
            Id = approval.Id,
            RecommendationId = approval.RecommendationId,
            EventId = approval.EventId,
            CorrelationId = approval.CorrelationId,
            CustomerId = approval.CustomerId,
            SubscriptionId = approval.SubscriptionId,
            EndpointId = approval.EndpointId,
            RecommendationType = approval.RecommendationType,
            ApprovalStatus = approval.ApprovalStatus,
            RiskLevel = approval.RiskLevel,
            SuggestedAction = approval.SuggestedAction,
            Summary = approval.Summary,
            Recommendation = approval.Recommendation,
            RequestedBy = approval.RequestedBy,
            ReviewedBy = approval.ReviewedBy,
            ReviewComment = approval.ReviewComment,
            RequiresApproval = approval.RequiresApproval,
            CreatedAtUtc = DateTime.SpecifyKind(approval.CreatedAtUtc, DateTimeKind.Utc),
            ReviewedAtUtc = SpecifyUtc(approval.ReviewedAtUtc),
            AppliedAtUtc = SpecifyUtc(approval.AppliedAtUtc),
            ExpiresAtUtc = SpecifyUtc(approval.ExpiresAtUtc)
        };
    }

    public static AiRecommendationApprovalStatusUpdate ToStatusUpdate(
        AiRecommendationApprovalUpdateRequestDto request,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        return new AiRecommendationApprovalStatusUpdate
        {
            ApprovalStatus = request.ApprovalStatus!.Value,
            ReviewedBy = TrimToNull(request.ReviewedBy),
            ReviewComment = TrimToNull(request.ReviewComment),
            ReviewedAtUtc = request.ApprovalStatus == AiRecommendationApprovalStatus.Applied ? null : nowUtc,
            AppliedAtUtc = request.ApprovalStatus == AiRecommendationApprovalStatus.Applied ? nowUtc : null
        };
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? SpecifyUtc(DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
