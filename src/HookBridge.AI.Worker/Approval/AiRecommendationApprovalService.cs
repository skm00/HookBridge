using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Approval;

public sealed class AiRecommendationApprovalService : IAiRecommendationApprovalService
{
    private readonly IAiRecommendationApprovalRepository _repository;
    private readonly AiRecommendationApprovalOptions _options;
    private readonly ILogger<AiRecommendationApprovalService> _logger;

    public AiRecommendationApprovalService(
        IAiRecommendationApprovalRepository repository,
        IOptions<AiRecommendationApprovalOptions> options,
        ILogger<AiRecommendationApprovalService> logger)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiRecommendationApprovalResponseDto> CreateAsync(AiRecommendationApprovalCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        var recommendationId = request.RecommendationId.Trim();
        var existing = await _repository.GetByRecommendationIdAsync(recommendationId, cancellationToken);
        if (existing is not null)
        {
            _logger.LogWarning(
                "Duplicate AI recommendation approval create rejected. RecommendationId={RecommendationId} ExistingApprovalId={ApprovalId}",
                recommendationId,
                existing.Id);
            throw new AiRecommendationApprovalConflictException($"AI recommendation approval already exists for recommendation '{recommendationId}'.");
        }

        var nowUtc = DateTime.UtcNow;
        var approval = AiRecommendationApprovalMapper.ToEntity(request, _options, nowUtc);

        await _repository.InsertAsync(approval, cancellationToken);
        _logger.LogInformation(
            "AI recommendation approval record created. ApprovalId={ApprovalId} RecommendationId={RecommendationId} RecommendationType={RecommendationType} ApprovalStatus={ApprovalStatus} RiskLevel={RiskLevel}",
            approval.Id,
            approval.RecommendationId,
            approval.RecommendationType,
            approval.ApprovalStatus,
            approval.RiskLevel);

        return AiRecommendationApprovalMapper.ToResponseDto(approval);
    }

    public async Task<AiRecommendationApprovalResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var approval = await _repository.GetByIdAsync(id, cancellationToken);
        return approval is null ? null : AiRecommendationApprovalMapper.ToResponseDto(approval);
    }

    public async Task<AiRecommendationApprovalResponseDto?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendationId);
        var approval = await _repository.GetByRecommendationIdAsync(recommendationId, cancellationToken);
        return approval is null ? null : AiRecommendationApprovalMapper.ToResponseDto(approval);
    }

    public async Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 500.");
        }

        var approvals = await _repository.GetPendingAsync(limit, cancellationToken);
        _logger.LogInformation("Pending AI recommendation approvals queried. Count={Count} Limit={Limit}", approvals.Count, limit);
        return approvals.Select(AiRecommendationApprovalMapper.ToResponseDto).ToArray();
    }

    public async Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateSearchRequest(request);
        var approvals = await _repository.SearchAsync(request, cancellationToken);
        return approvals.Select(AiRecommendationApprovalMapper.ToResponseDto).ToArray();
    }

    public async Task<AiRecommendationApprovalResponseDto?> UpdateStatusAsync(string id, AiRecommendationApprovalUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ValidateUpdateRequest(request);

        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var requestedStatus = request.ApprovalStatus!.Value;
        if (IsActionableStatusExpired(existing, nowUtc) && requestedStatus != AiRecommendationApprovalStatus.Expired)
        {
            _logger.LogWarning(
                "Expired AI recommendation approval transition rejected. ApprovalId={ApprovalId} RecommendationId={RecommendationId} ApprovalStatus={ApprovalStatus} RequestedStatus={RequestedStatus} ExpiresAtUtc={ExpiresAtUtc}",
                existing.Id,
                existing.RecommendationId,
                existing.ApprovalStatus,
                requestedStatus,
                existing.ExpiresAtUtc);
            throw new AiRecommendationApprovalConflictException("AI recommendation approval has expired and can only transition to Expired.");
        }

        if (!AiRecommendationApprovalRules.CanTransition(existing.ApprovalStatus, requestedStatus))
        {
            _logger.LogWarning(
                "Invalid AI recommendation approval status transition. ApprovalId={ApprovalId} RecommendationId={RecommendationId} FromStatus={FromStatus} ToStatus={ToStatus}",
                existing.Id,
                existing.RecommendationId,
                existing.ApprovalStatus,
                requestedStatus);
            throw new AiRecommendationApprovalConflictException($"Invalid approval status transition from {existing.ApprovalStatus} to {requestedStatus}.");
        }

        var update = AiRecommendationApprovalMapper.ToStatusUpdate(request, nowUtc);
        var updated = await _repository.UpdateStatusAsync(id, update, cancellationToken);
        if (updated is null)
        {
            return null;
        }

        _logger.LogInformation(
            "AI recommendation approval status updated. ApprovalId={ApprovalId} RecommendationId={RecommendationId} ApprovalStatus={ApprovalStatus}",
            updated.Id,
            updated.RecommendationId,
            updated.ApprovalStatus);

        return AiRecommendationApprovalMapper.ToResponseDto(updated);
    }

    private static bool IsActionableStatusExpired(AiRecommendationApproval approval, DateTime nowUtc)
        => approval.ExpiresAtUtc.HasValue
           && approval.ExpiresAtUtc.Value <= nowUtc
           && approval.ApprovalStatus is AiRecommendationApprovalStatus.PendingReview or AiRecommendationApprovalStatus.Approved;

    private static void ValidateCreateRequest(AiRecommendationApprovalCreateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RecommendationId)) throw new ArgumentException("RecommendationId is required.", nameof(request));
        if (!request.RecommendationType.HasValue || !Enum.IsDefined(request.RecommendationType.Value)) throw new ArgumentException("RecommendationType is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RiskLevel)) throw new ArgumentException("RiskLevel is required.", nameof(request));
    }

    private static void ValidateUpdateRequest(AiRecommendationApprovalUpdateRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.ApprovalStatus.HasValue || !Enum.IsDefined(request.ApprovalStatus.Value)) throw new ArgumentException("ApprovalStatus is required.", nameof(request));
    }

    private static void ValidateSearchRequest(AiRecommendationApprovalSearchRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(request), "PageNumber must be greater than 0.");
        if (request.PageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(request), "PageSize must be between 1 and 500.");
        ValidateUtc(request.FromUtc, nameof(request.FromUtc));
        ValidateUtc(request.ToUtc, nameof(request.ToUtc));
    }

    private static void ValidateUtc(DateTime? value, string name)
    {
        if (value.HasValue && value.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"{name} must be UTC.", name);
        }
    }
}
