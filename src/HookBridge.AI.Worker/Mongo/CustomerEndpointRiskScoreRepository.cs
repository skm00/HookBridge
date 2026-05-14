using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class CustomerEndpointRiskScoreRepository : ICustomerEndpointRiskScoreRepository
{
    private readonly IMongoCollection<CustomerEndpointRiskScoreResult> _collection;

    public CustomerEndpointRiskScoreRepository(ICustomerEndpointRiskScoreCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(CustomerEndpointRiskScoreResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CalculatedAtUtc = DateTime.SpecifyKind(result.CalculatedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<CustomerEndpointRiskScoreResult>.Filter.Eq(result => result.CustomerId, customerId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<CustomerEndpointRiskScoreResult>.Filter.Eq(result => result.SubscriptionId, subscriptionId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<CustomerEndpointRiskScoreResult>.Filter.Eq(result => result.EndpointId, endpointId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>(Array.Empty<CustomerEndpointRiskScoreResult>());
        return ToListAsync(Builders<CustomerEndpointRiskScoreResult>.Filter.Empty, Builders<CustomerEndpointRiskScoreResult>.Sort.Descending(result => result.CalculatedAtUtc), limit, cancellationToken);
    }


    public Task<long> CountHighRiskEndpointsAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var mongoFilter = BuildDashboardFilter(filter);
        var builder = Builders<CustomerEndpointRiskScoreResult>.Filter;
        mongoFilter &= builder.In(result => result.RiskLevel, new[] { "High", "high", "HIGH", "Critical", "critical", "CRITICAL" });
        return _collection.CountDocumentsAsync(mongoFilter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountByHealthStatusAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);
        return results.GroupBy(result => NormalizeBucket(result.HealthStatus))
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> ToListAsync(FilterDefinition<CustomerEndpointRiskScoreResult> filter, SortDefinition<CustomerEndpointRiskScoreResult>? sort = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var options = new FindOptions<CustomerEndpointRiskScoreResult> { Sort = sort, Limit = limit };
        var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
    private static FilterDefinition<CustomerEndpointRiskScoreResult> BuildDashboardFilter(AiDashboardQueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var builder = Builders<CustomerEndpointRiskScoreResult>.Filter;
        var mongoFilter = builder.Gte(result => result.CalculatedAtUtc, filter.FromUtc) & builder.Lt(result => result.CalculatedAtUtc, filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.Environment)) mongoFilter &= builder.Eq(result => result.Environment, filter.Environment);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) mongoFilter &= builder.Eq(result => result.CustomerId, filter.CustomerId);
        if (!string.IsNullOrWhiteSpace(filter.CustomerIdType)) mongoFilter &= builder.Eq(result => result.CustomerIdType, filter.CustomerIdType);
        if (!string.IsNullOrWhiteSpace(filter.SubscriptionId)) mongoFilter &= builder.Eq(result => result.SubscriptionId, filter.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(filter.EndpointId)) mongoFilter &= builder.Eq(result => result.EndpointId, filter.EndpointId);
        return mongoFilter;
    }

    private static string NormalizeBucket(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();

}
