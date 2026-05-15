using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class RetryAgentResultRepository : IRetryAgentResultRepository
{
    private readonly IMongoCollection<RetryAgentResult> _collection;
    public RetryAgentResultRepository(IRetryAgentResultCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(RetryAgentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.FailedAtUtc = DateTime.SpecifyKind(result.FailedAtUtc, DateTimeKind.Utc);
        if (result.LastRetryAtUtc.HasValue) result.LastRetryAtUtc = DateTime.SpecifyKind(result.LastRetryAtUtc.Value, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<RetryAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<RetryAgentResult>.Filter.Eq(result => result.EventId, eventId), Builders<RetryAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<RetryAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<RetryAgentResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<RetryAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<RetryAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<RetryAgentResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<RetryAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<RetryAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<RetryAgentResult>>(Array.Empty<RetryAgentResult>()) : ToListAsync(Builders<RetryAgentResult>.Filter.Empty, Builders<RetryAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<RetryAgentResult>> SearchAsync(RetryAgentSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<RetryAgentResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.RetryDecision is not null) filter &= builder.Eq(result => result.RetryDecision, request.RetryDecision.Value);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel);
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<RetryAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<RetryAgentResult>> ToListAsync(FilterDefinition<RetryAgentResult> filter, SortDefinition<RetryAgentResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<RetryAgentResult, RetryAgentResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
