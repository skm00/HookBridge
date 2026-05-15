using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookTransformationRecommendationRepository : IWebhookTransformationRecommendationRepository
{
    private readonly IMongoCollection<WebhookTransformationRecommendationResult> _collection;
    public WebhookTransformationRecommendationRepository(IWebhookTransformationRecommendationCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();
    public Task InsertAsync(WebhookTransformationRecommendationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public async Task<WebhookTransformationRecommendationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        var results = await ToListAsync(
            Builders<WebhookTransformationRecommendationResult>.Filter.Eq(result => result.EventId, eventId),
            Builders<WebhookTransformationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc),
            1,
            cancellationToken);
        return results.FirstOrDefault();
    }

    public Task<IReadOnlyList<WebhookTransformationRecommendationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
        => ToListAsync(
            Builders<WebhookTransformationRecommendationResult>.Filter.Eq(result => result.CorrelationId, correlationId),
            Builders<WebhookTransformationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc),
            null,
            cancellationToken);

    public Task<IReadOnlyList<WebhookTransformationRecommendationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => limit <= 0
            ? Task.FromResult<IReadOnlyList<WebhookTransformationRecommendationResult>>(Array.Empty<WebhookTransformationRecommendationResult>())
            : ToListAsync(Builders<WebhookTransformationRecommendationResult>.Filter.Empty, Builders<WebhookTransformationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);

    public Task<IReadOnlyList<WebhookTransformationRecommendationResult>> SearchAsync(WebhookTransformationRecommendationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = Builders<WebhookTransformationRecommendationResult>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrWhiteSpace(request.CustomerId)) filter &= builder.Eq(result => result.CustomerId, request.CustomerId);
        if (!string.IsNullOrWhiteSpace(request.EventType)) filter &= builder.Eq(result => result.EventType, request.EventType);
        if (!string.IsNullOrWhiteSpace(request.RiskLevel)) filter &= builder.Eq(result => result.RiskLevel, request.RiskLevel);
        if (request.FromUtc is not null) filter &= builder.Gte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.FromUtc.Value, DateTimeKind.Utc));
        if (request.ToUtc is not null) filter &= builder.Lte(result => result.GeneratedAtUtc, DateTime.SpecifyKind(request.ToUtc.Value, DateTimeKind.Utc));
        var limit = request.Limit <= 0 ? 100 : Math.Min(request.Limit, 1000);
        return ToListAsync(filter, Builders<WebhookTransformationRecommendationResult>.Sort.Descending(result => result.GeneratedAtUtc), limit, cancellationToken);
    }

    private async Task<IReadOnlyList<WebhookTransformationRecommendationResult>> ToListAsync(
        FilterDefinition<WebhookTransformationRecommendationResult> filter,
        SortDefinition<WebhookTransformationRecommendationResult>? sort,
        int? limit,
        CancellationToken cancellationToken)
    {
        var cursor = await _collection.FindAsync(filter, new FindOptions<WebhookTransformationRecommendationResult, WebhookTransformationRecommendationResult> { Sort = sort, Limit = limit }, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
