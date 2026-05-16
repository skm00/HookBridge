using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class TransformationAgentResultRepository : ITransformationAgentResultRepository
{
    private readonly IMongoCollection<TransformationAgentResult> _collection;
    public TransformationAgentResultRepository(ITransformationAgentResultCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(TransformationAgentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.ReceivedAtUtc = DateTime.SpecifyKind(result.ReceivedAtUtc, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<TransformationAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<TransformationAgentResult>.Filter.Eq(result => result.EventId, eventId), Builders<TransformationAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<TransformationAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<TransformationAgentResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<TransformationAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<TransformationAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<TransformationAgentResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<TransformationAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<TransformationAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<TransformationAgentResult>>(Array.Empty<TransformationAgentResult>()) : ToListAsync(Builders<TransformationAgentResult>.Filter.Empty, Builders<TransformationAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<TransformationAgentResult>> SearchAsync(TransformationAgentSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<TransformationAgentResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.TransformationDecision is not null) filter &= builder.Eq(result => result.TransformationDecision, request.TransformationDecision.Value);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel);
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<TransformationAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<TransformationAgentResult>> ToListAsync(FilterDefinition<TransformationAgentResult> filter, SortDefinition<TransformationAgentResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<TransformationAgentResult, TransformationAgentResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
