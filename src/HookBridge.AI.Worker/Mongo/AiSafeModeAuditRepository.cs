using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSafeModeAuditRepository : IAiSafeModeAuditRepository
{
    private readonly IMongoCollection<AiSafeModeAuditRecord> _collection;

    public AiSafeModeAuditRepository(IAiSafeModeAuditRecordCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(AiSafeModeAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.EvaluatedAtUtc = DateTime.SpecifyKind(record.EvaluatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(record, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiSafeModeAuditRecord>.Filter.Eq(record => record.EventId, eventId), 100, cancellationToken);

    public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiSafeModeAuditRecord>.Filter.Eq(record => record.CorrelationId, correlationId), 100, cancellationToken);

    public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<AiSafeModeAuditRecord>>(Array.Empty<AiSafeModeAuditRecord>()) : ToListAsync(Builders<AiSafeModeAuditRecord>.Filter.Empty, limit, cancellationToken);

    public Task<IReadOnlyList<AiSafeModeAuditRecord>> SearchAsync(AiSafeModeAuditSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<AiSafeModeAuditRecord>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(record => record.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(record => record.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(record => record.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(record => record.Environment, request.Environment);
        if (request.ActionType is not null) filter &= builder.Eq(record => record.ActionType, request.ActionType.Value);
        if (request.Decision is not null) filter &= builder.Eq(record => record.Decision, request.Decision.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(record => record.EvaluatedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(record => record.EvaluatedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<AiSafeModeAuditRecord>> ToListAsync(FilterDefinition<AiSafeModeAuditRecord> filter, int limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<AiSafeModeAuditRecord, AiSafeModeAuditRecord>
        {
            Sort = Builders<AiSafeModeAuditRecord>.Sort.Descending(record => record.EvaluatedAtUtc),
            Limit = limit
        };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
