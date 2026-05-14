using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSecurityAnalysisRepository : IAiSecurityAnalysisRepository
{
    private readonly IMongoCollection<AiSecurityAnalysisResult> _collection;
    public AiSecurityAnalysisRepository(IAiSecurityAnalysisCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();

    public Task InsertAsync(AiSecurityAnalysisResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        result.ReceivedAtUtc = DateTime.SpecifyKind(result.ReceivedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<AiSecurityAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(Builders<AiSecurityAnalysisResult>.Filter.Eq(result => result.EventId, eventId), Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), 1, cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiSecurityAnalysisResult>.Filter.Eq(result => result.CorrelationId, correlationId), Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<AiSecurityAnalysisResult>.Filter.Eq(result => result.CustomerId, customerId), Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);

    public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0 ? Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>(Array.Empty<AiSecurityAnalysisResult>()) : ToListAsync(Builders<AiSecurityAnalysisResult>.Filter.Empty, Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<AiSecurityAnalysisResult>> SearchAsync(AiSecurityAnalysisSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<AiSecurityAnalysisResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) filter &= builder.Eq(result => result.SubscriptionId, request.SubscriptionId);
        if (!string.IsNullOrWhiteSpace(request.EndpointId)) filter &= builder.Eq(result => result.EndpointId, request.EndpointId);
        if (!string.IsNullOrWhiteSpace(request.Environment)) filter &= builder.Eq(result => result.Environment, request.Environment);
        if (request.RiskLevel is not null) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel.Value.ToString());
        if (request.IsSuspicious is not null) filter &= builder.Eq(result => result.IsSuspicious, request.IsSuspicious.Value);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }


    public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
        => _collection.CountDocumentsAsync(BuildDashboardFilter(filter), cancellationToken: cancellationToken);

    public async Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);
        return results.GroupBy(result => NormalizeBucket(result.RiskLevel))
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(BuildDashboardFilter(filter), Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc), null, cancellationToken);
        return results.Count == 0 ? 0 : Math.Round(results.Average(result => result.ConfidenceScore), 4);
    }

    public async Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Array.Empty<AiDashboardRecentFindingResult>();

        var results = await ToListAsync(
            BuildDashboardFilter(filter),
            Builders<AiSecurityAnalysisResult>.Sort.Descending(result => result.GeneratedAtUtc),
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
            FindingType = "Security",
            Title = result.IsSuspicious ? "Suspicious webhook activity detected" : "Security analysis completed",
            Summary = result.Summary,
            RiskLevel = result.RiskLevel,
            SuggestedAction = result.SuggestedAction,
            CreatedAtUtc = result.GeneratedAtUtc
        }).ToList();
    }

    private async Task<IReadOnlyList<AiSecurityAnalysisResult>> ToListAsync(FilterDefinition<AiSecurityAnalysisResult> filter, SortDefinition<AiSecurityAnalysisResult>? sort, int? limit, CancellationToken cancellationToken)
    {
        var cursor = await _collection.FindAsync(filter, new FindOptions<AiSecurityAnalysisResult> { Sort = sort, Limit = limit }, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
    private static FilterDefinition<AiSecurityAnalysisResult> BuildDashboardFilter(AiDashboardQueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var builder = Builders<AiSecurityAnalysisResult>.Filter;
        var mongoFilter = builder.Gte(result => result.GeneratedAtUtc, filter.FromUtc) & builder.Lt(result => result.GeneratedAtUtc, filter.ToUtc);
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
