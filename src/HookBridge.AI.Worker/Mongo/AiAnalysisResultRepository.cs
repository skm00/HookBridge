using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAnalysisResultRepository : IAiAnalysisResultRepository
{
    private readonly IMongoCollection<AiAnalysisResult> _collection;

    public AiAnalysisResultRepository(IAiAnalysisResultCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiAnalysisResult>.Filter.Eq(result => result.Id, id);
        return await FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiAnalysisResult>.Filter.Eq(result => result.EventId, eventId);
        return await FirstOrDefaultAsync(filter, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiAnalysisResult>.Filter.Eq(result => result.CorrelationId, correlationId);
        return await ToListAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<AiAnalysisResult>();
        }

        return await ToListAsync(
            Builders<AiAnalysisResult>.Filter.Empty,
            Builders<AiAnalysisResult>.Sort.Descending(result => result.CreatedAtUtc),
            limit,
            cancellationToken);
    }

    private async Task<AiAnalysisResult?> FirstOrDefaultAsync(
        FilterDefinition<AiAnalysisResult> filter,
        CancellationToken cancellationToken)
    {
        var results = await ToListAsync(filter, limit: 1, cancellationToken: cancellationToken);
        return results.FirstOrDefault();
    }

    private async Task<IReadOnlyList<AiAnalysisResult>> ToListAsync(
        FilterDefinition<AiAnalysisResult> filter,
        SortDefinition<AiAnalysisResult>? sort = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var options = new FindOptions<AiAnalysisResult>
        {
            Sort = sort,
            Limit = limit
        };

        using var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
