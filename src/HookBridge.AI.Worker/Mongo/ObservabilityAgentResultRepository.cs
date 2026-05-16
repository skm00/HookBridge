using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class ObservabilityAgentResultRepository : IObservabilityAgentResultRepository
{
    private readonly IMongoCollection<ObservabilityAgentResult> _collection;
    public ObservabilityAgentResultRepository(IObservabilityAgentResultCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(ObservabilityAgentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.EvaluationWindowFromUtc = DateTime.SpecifyKind(result.EvaluationWindowFromUtc, DateTimeKind.Utc);
        result.EvaluationWindowToUtc = DateTime.SpecifyKind(result.EvaluationWindowToUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<ObservabilityAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<ObservabilityAgentResult>.Filter.Eq(result => result.EventId, eventId), Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<ObservabilityAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<ObservabilityAgentResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<ObservabilityAgentResult>> GetByEnvironmentAsync(string environment, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<ObservabilityAgentResult>.Filter.Eq(result => result.Environment, environment), Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<ObservabilityAgentResult>> GetByServiceNameAsync(string serviceName, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<ObservabilityAgentResult>.Filter.Eq(result => result.ServiceName, serviceName), Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<ObservabilityAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<ObservabilityAgentResult>>(Array.Empty<ObservabilityAgentResult>()) : ToListAsync(Builders<ObservabilityAgentResult>.Filter.Empty, Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<ObservabilityAgentResult>> SearchAsync(ObservabilityAgentSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<ObservabilityAgentResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (!string.IsNullOrWhiteSpace(request.ServiceName)) filter &= builder.Eq(result => result.ServiceName, request.ServiceName);
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (request.ObservabilityStatus is not null) filter &= builder.Eq(result => result.ObservabilityStatus, request.ObservabilityStatus.Value);
        if (request.RiskLevel is not null) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<ObservabilityAgentResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<ObservabilityAgentResult>> ToListAsync(FilterDefinition<ObservabilityAgentResult> filter, SortDefinition<ObservabilityAgentResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<ObservabilityAgentResult, ObservabilityAgentResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
