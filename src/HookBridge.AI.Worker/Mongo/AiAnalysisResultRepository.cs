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


    public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
        => _collection.CountDocumentsAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);

    public async Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);
        return results.GroupBy(result => NormalizeBucket(result.RiskLevel))
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, long>> CountByRetryActionAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);
        return results.GroupBy(result => NormalizeBucket(result.SuggestedRetryAction))
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);
        return results.Count == 0 ? 0 : Math.Round(results.Average(result => result.ConfidenceScore), 4);
    }

    public async Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Array.Empty<AiDashboardRecentFindingResult>();

        var results = await ToListAsync(
            BuildDashboardFilter(filter),
            Builders<AiAnalysisResult>.Sort.Descending(result => result.CreatedAtUtc),
            limit,
            cancellationToken);

        return results.Select(result => new AiDashboardRecentFindingResult
        {
            Id = result.Id,
            EventId = result.EventId,
            CorrelationId = result.CorrelationId,
            CustomerId = result.CustomerId,
            SubscriptionId = result.SubscriptionId,
            EndpointId = result.EndpointId,
            FindingType = "Analysis",
            Title = string.IsNullOrWhiteSpace(result.RootCause) ? "AI analysis completed" : result.RootCause,
            Summary = result.AiSummary,
            RiskLevel = result.RiskLevel,
            SuggestedAction = result.SuggestedRetryAction,
            CreatedAtUtc = result.CreatedAtUtc
        }).ToList();
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

        var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
    private static FilterDefinition<AiAnalysisResult> BuildDashboardFilter(AiDashboardQueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var builder = Builders<AiAnalysisResult>.Filter;
        var mongoFilter = builder.Gte(result => result.CreatedAtUtc, filter.FromUtc) & builder.Lt(result => result.CreatedAtUtc, filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.Environment)) mongoFilter &= builder.Eq(result => result.Environment, filter.Environment);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) mongoFilter &= builder.Eq(result => result.CustomerId, filter.CustomerId);
        if (!string.IsNullOrWhiteSpace(filter.CustomerIdType)) mongoFilter &= builder.Eq(result => result.CustomerIdType, filter.CustomerIdType);
        if (!string.IsNullOrWhiteSpace(filter.SubscriptionId)) mongoFilter &= builder.Eq(result => result.SubscriptionId, filter.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(filter.EndpointId)) mongoFilter &= builder.Eq(result => result.EndpointId, filter.EndpointId);
        if (!string.IsNullOrWhiteSpace(filter.EventType)) mongoFilter &= builder.Eq(result => result.EventType, filter.EventType);
        return mongoFilter;
    }

    private static string NormalizeBucket(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
}
