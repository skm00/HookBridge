using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiRecommendationApprovalRepository : IAiRecommendationApprovalRepository
{
    private readonly IMongoCollection<AiRecommendationApproval> _collection;

    public AiRecommendationApprovalRepository(IAiRecommendationApprovalCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(AiRecommendationApproval approval, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ValidateApproval(approval);
        approval.CreatedAtUtc = DateTime.SpecifyKind(approval.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(approval, cancellationToken: cancellationToken);
    }

    public Task<AiRecommendationApproval?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(Builders<AiRecommendationApproval>.Filter.Eq(approval => approval.Id, id), cancellationToken);

    public Task<AiRecommendationApproval?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(Builders<AiRecommendationApproval>.Filter.Eq(approval => approval.RecommendationId, recommendationId), cancellationToken);

    public Task<IReadOnlyList<AiRecommendationApproval>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiRecommendationApproval>.Filter.Eq(approval => approval.EventId, eventId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<AiRecommendationApproval>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
        => ToListAsync(
            Builders<AiRecommendationApproval>.Filter.Eq(approval => approval.ApprovalStatus, AiRecommendationApprovalStatus.PendingReview),
            Builders<AiRecommendationApproval>.Sort.Descending(approval => approval.CreatedAtUtc),
            limit,
            null,
            cancellationToken);

    public Task<IReadOnlyList<AiRecommendationApproval>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSearchRequest(request);

        var skip = (request.PageNumber - 1) * request.PageSize;
        return ToListAsync(
            BuildSearchFilter(request),
            Builders<AiRecommendationApproval>.Sort.Descending(approval => approval.CreatedAtUtc),
            request.PageSize,
            skip,
            cancellationToken);
    }

    public async Task<AiRecommendationApproval?> UpdateStatusAsync(string id, AiRecommendationApprovalStatusUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(update);
        ValidateUtc(update.ReviewedAtUtc, nameof(update.ReviewedAtUtc));
        ValidateUtc(update.AppliedAtUtc, nameof(update.AppliedAtUtc));

        var updates = Builders<AiRecommendationApproval>.Update
            .Set(approval => approval.ApprovalStatus, update.ApprovalStatus)
            .Set(approval => approval.ReviewedBy, update.ReviewedBy)
            .Set(approval => approval.ReviewComment, update.ReviewComment)
            .Set(approval => approval.ReviewedAtUtc, update.ReviewedAtUtc)
            .Set(approval => approval.AppliedAtUtc, update.AppliedAtUtc);

        return await _collection.FindOneAndUpdateAsync(
            Builders<AiRecommendationApproval>.Filter.Eq(approval => approval.Id, id),
            updates,
            new FindOneAndUpdateOptions<AiRecommendationApproval> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    private static FilterDefinition<AiRecommendationApproval> BuildSearchFilter(AiRecommendationApprovalSearchRequestDto request)
    {
        var builder = Builders<AiRecommendationApproval>.Filter;
        var filters = new List<FilterDefinition<AiRecommendationApproval>>();

        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filters.Add(builder.Eq(approval => approval.CustomerId, request.CustomerId));
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filters.Add(builder.Eq(approval => approval.SubscriptionId, request.SubscriptionId));
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filters.Add(builder.Eq(approval => approval.EndpointId, request.EndpointId));
        if (request.RecommendationType.HasValue) filters.Add(builder.Eq(approval => approval.RecommendationType, request.RecommendationType.Value));
        if (request.ApprovalStatus.HasValue) filters.Add(builder.Eq(approval => approval.ApprovalStatus, request.ApprovalStatus.Value));
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filters.Add(builder.Eq(approval => approval.RiskLevel, request.RiskLevel));
        if (request.FromUtc.HasValue) filters.Add(builder.Gte(approval => approval.CreatedAtUtc, request.FromUtc.Value));
        if (request.ToUtc.HasValue) filters.Add(builder.Lte(approval => approval.CreatedAtUtc, request.ToUtc.Value));

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private static void ValidateApproval(AiRecommendationApproval approval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approval.RecommendationId);
        ValidateUtc(approval.CreatedAtUtc, nameof(approval.CreatedAtUtc));
        ValidateUtc(approval.ReviewedAtUtc, nameof(approval.ReviewedAtUtc));
        ValidateUtc(approval.AppliedAtUtc, nameof(approval.AppliedAtUtc));
        ValidateUtc(approval.ExpiresAtUtc, nameof(approval.ExpiresAtUtc));
    }

    private static void ValidateSearchRequest(AiRecommendationApprovalSearchRequestDto request)
    {
        if (request.PageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(request), "PageNumber must be greater than 0.");
        if (request.PageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(request), "PageSize must be between 1 and 500.");
        ValidateUtc(request.FromUtc, nameof(request.FromUtc));
        ValidateUtc(request.ToUtc, nameof(request.ToUtc));
    }

    private static void ValidateUtc(DateTime? value, string name)
    {
        if (value.HasValue)
        {
            ValidateUtc(value.Value, name);
        }
    }

    private static void ValidateUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"{name} must be UTC.", name);
        }
    }

    private async Task<AiRecommendationApproval?> FirstOrDefaultAsync(FilterDefinition<AiRecommendationApproval> filter, CancellationToken cancellationToken)
    {
        using var cursor = await _collection.FindAsync(filter, new FindOptions<AiRecommendationApproval> { Limit = 1 }, cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    private Task<IReadOnlyList<AiRecommendationApproval>> ToListAsync(
        FilterDefinition<AiRecommendationApproval> filter,
        SortDefinition<AiRecommendationApproval>? sort = null,
        int? limit = null,
        int? skip = null,
        CancellationToken cancellationToken = default)
    {
        return ToListCoreAsync(filter, sort, limit, skip, cancellationToken);
    }

    private async Task<IReadOnlyList<AiRecommendationApproval>> ToListCoreAsync(
        FilterDefinition<AiRecommendationApproval> filter,
        SortDefinition<AiRecommendationApproval>? sort,
        int? limit,
        int? skip,
        CancellationToken cancellationToken)
    {
        var options = new FindOptions<AiRecommendationApproval>
        {
            Sort = sort,
            Limit = limit,
            Skip = skip
        };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
