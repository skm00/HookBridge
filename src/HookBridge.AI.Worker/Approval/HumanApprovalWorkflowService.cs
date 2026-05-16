using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Approval;

public sealed class HumanApprovalWorkflowService : IHumanApprovalWorkflowService
{
    private readonly IAiRecommendationApprovalRepository _repository;
    private readonly HumanApprovalWorkflowOptions _options;
    private readonly ILogger<HumanApprovalWorkflowService> _logger;

    public HumanApprovalWorkflowService(
        IAiRecommendationApprovalRepository repository,
        IOptions<HumanApprovalWorkflowOptions> options,
        ILogger<HumanApprovalWorkflowService> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HumanApprovalWorkflowResponseDto> CreateAsync(HumanApprovalWorkflowCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        var recommendationId = request.RecommendationId.Trim();
        var existing = await _repository.GetByRecommendationIdAsync(recommendationId, cancellationToken);
        if (existing is not null)
        {
            throw new AiRecommendationApprovalConflictException($"Human approval workflow already exists for recommendation '{recommendationId}'.");
        }

        var requiresApproval = HumanApprovalWorkflowRules.RequiresApproval(request.RecommendationType!.Value, request.RiskLevel, request.SuggestedAction, _options);
        var approval = new AiRecommendationApproval
        {
            RecommendationId = recommendationId,
            RecommendationType = request.RecommendationType.Value,
            EventId = TrimToNull(request.EventId),
            CorrelationId = TrimToNull(request.CorrelationId),
            CustomerId = TrimToNull(request.CustomerId),
            CustomerIdType = TrimToNull(request.CustomerIdType),
            SubscriptionId = TrimToNull(request.SubscriptionId),
            EndpointId = TrimToNull(request.EndpointId),
            Environment = TrimToNull(request.Environment),
            RiskLevel = request.RiskLevel.Trim(),
            SuggestedAction = TrimToNull(request.SuggestedAction),
            Summary = request.Summary?.Trim() ?? string.Empty,
            Recommendation = request.Recommendation?.Trim() ?? string.Empty,
            RequestedBy = request.RequestedBy.Trim(),
            RequiresApproval = requiresApproval,
            ApprovalStatus = requiresApproval ? AiRecommendationApprovalStatus.PendingReview : AiRecommendationApprovalStatus.Approved,
            CreatedAtUtc = DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(request.CreatedAtUtc.AddHours(_options.ApprovalExpiryHours), DateTimeKind.Utc)
        };

        await _repository.InsertAsync(approval, cancellationToken);
        _logger.LogInformation(
            "Approval workflow created. ApprovalId={ApprovalId} RecommendationId={RecommendationId} RecommendationType={RecommendationType} ApprovalStatus={ApprovalStatus} RiskLevel={RiskLevel} RequiresApproval={RequiresApproval}",
            approval.Id,
            approval.RecommendationId,
            approval.RecommendationType,
            approval.ApprovalStatus,
            approval.RiskLevel,
            approval.RequiresApproval);

        return ToResponse(approval);
    }

    public async Task<HumanApprovalWorkflowResponseDto?> GetByIdAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        var approval = await _repository.GetByIdAsync(approvalId, cancellationToken);
        return approval is null ? null : ToResponse(approval);
    }

    public async Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
        var approvals = await _repository.GetPendingAsync(limit, cancellationToken);
        return approvals.Select(ToResponse).ToArray();
    }

    public async Task<HumanApprovalWorkflowResponseDto?> ReviewAsync(string approvalId, HumanApprovalWorkflowReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ValidateReviewRequest(request);
        if (request.ApprovalStatus == AiRecommendationApprovalStatus.Applied)
        {
            throw new AiRecommendationApprovalConflictException("Approved recommendations must be applied using the apply endpoint.");
        }

        return await TransitionAsync(
            approvalId,
            request.ApprovalStatus!.Value,
            reviewedBy: request.ReviewedBy,
            reviewComment: request.ReviewComment,
            appliedBy: null,
            applyComment: null,
            cancellationToken);
    }

    public async Task<HumanApprovalWorkflowResponseDto?> ApplyAsync(string approvalId, HumanApprovalWorkflowApplyRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ValidateApplyRequest(request);
        return await TransitionAsync(
            approvalId,
            AiRecommendationApprovalStatus.Applied,
            reviewedBy: null,
            reviewComment: null,
            appliedBy: request.AppliedBy,
            applyComment: request.ApplyComment,
            cancellationToken);
    }

    public Task<HumanApprovalWorkflowResponseDto?> ExpireAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        return TransitionAsync(approvalId, AiRecommendationApprovalStatus.Expired, null, null, null, null, cancellationToken);
    }

    private async Task<HumanApprovalWorkflowResponseDto?> TransitionAsync(
        string approvalId,
        AiRecommendationApprovalStatus requestedStatus,
        string? reviewedBy,
        string? reviewComment,
        string? appliedBy,
        string? applyComment,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(approvalId, cancellationToken);
        if (existing is null) return null;

        if (!HumanApprovalWorkflowRules.CanTransition(existing.ApprovalStatus, requestedStatus))
        {
            _logger.LogWarning(
                "Invalid transition attempted. ApprovalId={ApprovalId} RecommendationId={RecommendationId} FromStatus={FromStatus} ToStatus={ToStatus}",
                existing.Id,
                existing.RecommendationId,
                existing.ApprovalStatus,
                requestedStatus);
            throw new AiRecommendationApprovalConflictException($"Invalid approval status transition from {existing.ApprovalStatus} to {requestedStatus}.");
        }

        var nowUtc = DateTime.UtcNow;
        var update = new AiRecommendationApprovalStatusUpdate
        {
            ApprovalStatus = requestedStatus,
            ReviewedBy = requestedStatus == AiRecommendationApprovalStatus.Applied ? existing.ReviewedBy : TrimToNull(reviewedBy),
            ReviewComment = requestedStatus == AiRecommendationApprovalStatus.Applied ? existing.ReviewComment : TrimToNull(reviewComment),
            AppliedBy = requestedStatus == AiRecommendationApprovalStatus.Applied ? TrimToNull(appliedBy) : existing.AppliedBy,
            ApplyComment = requestedStatus == AiRecommendationApprovalStatus.Applied ? TrimToNull(applyComment) : existing.ApplyComment,
            ReviewedAtUtc = requestedStatus == AiRecommendationApprovalStatus.Applied ? existing.ReviewedAtUtc : nowUtc,
            AppliedAtUtc = requestedStatus == AiRecommendationApprovalStatus.Applied ? nowUtc : existing.AppliedAtUtc
        };

        var updated = await _repository.UpdateStatusAsync(approvalId, update, cancellationToken);
        if (updated is null) return null;

        LogTransition(updated);
        return ToResponse(updated);
    }

    private void LogTransition(AiRecommendationApproval approval)
    {
        var message = approval.ApprovalStatus switch
        {
            AiRecommendationApprovalStatus.Applied => "Approval applied. ApprovalId={ApprovalId} RecommendationId={RecommendationId}",
            AiRecommendationApprovalStatus.Expired => "Approval expired. ApprovalId={ApprovalId} RecommendationId={RecommendationId}",
            _ => "Approval reviewed. ApprovalId={ApprovalId} RecommendationId={RecommendationId} ApprovalStatus={ApprovalStatus}"
        };

        if (approval.ApprovalStatus is AiRecommendationApprovalStatus.Applied or AiRecommendationApprovalStatus.Expired)
        {
            _logger.LogInformation(message, approval.Id, approval.RecommendationId);
        }
        else
        {
            _logger.LogInformation(message, approval.Id, approval.RecommendationId, approval.ApprovalStatus);
        }
    }

    private static HumanApprovalWorkflowResponseDto ToResponse(AiRecommendationApproval approval)
        => new()
        {
            ApprovalId = approval.Id,
            RecommendationId = approval.RecommendationId,
            RecommendationType = approval.RecommendationType,
            ApprovalStatus = approval.ApprovalStatus,
            RiskLevel = approval.RiskLevel,
            SuggestedAction = approval.SuggestedAction,
            RequiresApproval = approval.RequiresApproval,
            CanApply = HumanApprovalWorkflowRules.CanApply(approval.ApprovalStatus),
            Summary = approval.Summary,
            Recommendation = approval.Recommendation,
            RequestedBy = approval.RequestedBy,
            ReviewedBy = approval.ReviewedBy,
            ReviewComment = approval.ReviewComment,
            AppliedBy = approval.AppliedBy,
            ApplyComment = approval.ApplyComment,
            CreatedAtUtc = DateTime.SpecifyKind(approval.CreatedAtUtc, DateTimeKind.Utc),
            ReviewedAtUtc = SpecifyUtc(approval.ReviewedAtUtc),
            AppliedAtUtc = SpecifyUtc(approval.AppliedAtUtc),
            ExpiresAtUtc = SpecifyUtc(approval.ExpiresAtUtc)
        };

    private static void ValidateCreateRequest(HumanApprovalWorkflowCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RecommendationId)) throw new ArgumentException("RecommendationId is required.", nameof(request));
        if (!request.RecommendationType.HasValue || !Enum.IsDefined(request.RecommendationType.Value)) throw new ArgumentException("RecommendationType is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RiskLevel)) throw new ArgumentException("RiskLevel is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestedBy)) throw new ArgumentException("RequestedBy is required.", nameof(request));
        ValidateUtc(request.CreatedAtUtc, nameof(request.CreatedAtUtc));
    }

    private static void ValidateReviewRequest(HumanApprovalWorkflowReviewRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.ApprovalStatus.HasValue || !Enum.IsDefined(request.ApprovalStatus.Value)) throw new ArgumentException("ApprovalStatus is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ReviewedBy)) throw new ArgumentException("ReviewedBy is required.", nameof(request));
    }

    private static void ValidateApplyRequest(HumanApprovalWorkflowApplyRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.AppliedBy)) throw new ArgumentException("AppliedBy is required.", nameof(request));
    }

    private static void ValidateUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException($"{name} must be UTC.", name);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? SpecifyUtc(DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
