using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class DeadLetterAiAnalysisRepository : IDeadLetterAiAnalysisRepository
{
    private readonly IMongoCollection<DeadLetterAiAnalysisResult> _collection;
    public DeadLetterAiAnalysisRepository(IDeadLetterAiAnalysisCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(DeadLetterAiAnalysisResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc == default ? DateTime.UtcNow : result.CreatedAtUtc, DateTimeKind.Utc);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<DeadLetterAiAnalysisResult?> GetByDeadLetterIdAsync(string deadLetterId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<DeadLetterAiAnalysisResult>.Filter.Eq(result => result.DeadLetterId, deadLetterId), Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<DeadLetterAiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<DeadLetterAiAnalysisResult>.Filter.Eq(result => result.EventId, eventId), Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<DeadLetterAiAnalysisResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<DeadLetterAiAnalysisResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<DeadLetterAiAnalysisResult>>(Array.Empty<DeadLetterAiAnalysisResult>()) : ToListAsync(Builders<DeadLetterAiAnalysisResult>.Filter.Empty, Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> SearchAsync(DeadLetterAiAnalysisSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<DeadLetterAiAnalysisResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.ReplaySafety is not null) filter &= builder.Eq(result => result.ReplaySafety, request.ReplaySafety.Value);
        if (request.SuggestedAction is not null) filter &= builder.Eq(result => result.SuggestedAction, request.SuggestedAction.Value);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel);
        if (request.RequiresApproval is not null) filter &= builder.Eq(result => result.RequiresApproval, request.RequiresApproval.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<DeadLetterAiAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<DeadLetterAiAnalysisResult>> ToListAsync(FilterDefinition<DeadLetterAiAnalysisResult> filter, SortDefinition<DeadLetterAiAnalysisResult> sort, int? limit, CancellationToken cancellationToken)
    {
        var options = new FindOptions<DeadLetterAiAnalysisResult, DeadLetterAiAnalysisResult> { Sort = sort, Limit = limit };
        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
