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

    private async Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> ToListAsync(FilterDefinition<CustomerEndpointRiskScoreResult> filter, SortDefinition<CustomerEndpointRiskScoreResult>? sort = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var options = new FindOptions<CustomerEndpointRiskScoreResult> { Sort = sort, Limit = limit };
        var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
