using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AutoRemediationRecommendationRepository : IAutoRemediationRecommendationRepository
{
    private readonly IMongoCollection<AutoRemediationRecommendationResult> _collection;
    public AutoRemediationRecommendationRepository(IAutoRemediationRecommendationCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(AutoRemediationRecommendationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<AutoRemediationRecommendationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<AutoRemediationRecommendationResult>.Filter.Eq(result => result.EventId, eventId), Builders<AutoRemediationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AutoRemediationRecommendationResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<AutoRemediationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AutoRemediationRecommendationResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<AutoRemediationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<AutoRemediationRecommendationResult>>(Array.Empty<AutoRemediationRecommendationResult>()) : ToListAsync(Builders<AutoRemediationRecommendationResult>.Filter.Empty, Builders<AutoRemediationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<AutoRemediationRecommendationResult>> SearchAsync(AutoRemediationRecommendationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<AutoRemediationRecommendationResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.RemediationType is not null) filter &= builder.Eq(result => result.RemediationType, request.RemediationType.Value);
        if (request.RecommendedAction is not null) filter &= builder.Eq(result => result.RecommendedAction, request.RecommendedAction.Value);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel);
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<AutoRemediationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<AutoRemediationRecommendationResult>> ToListAsync(FilterDefinition<AutoRemediationRecommendationResult> filter, SortDefinition<AutoRemediationRecommendationResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<AutoRemediationRecommendationResult, AutoRemediationRecommendationResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
