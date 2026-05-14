using HookBridge.AI.Worker.DTOs;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookFailureAnomalyDetectionRepository : IWebhookFailureAnomalyDetectionRepository
{
    private readonly IMongoCollection<WebhookFailureAnomalyDetectionResult> _collection;

    public WebhookFailureAnomalyDetectionRepository(IWebhookFailureAnomalyDetectionCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(WebhookFailureAnomalyDetectionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CalculatedAtUtc = DateTime.SpecifyKind(result.CalculatedAtUtc, DateTimeKind.Utc);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<WebhookFailureAnomalyDetectionResult>.Filter.Eq(result => result.CustomerId, customerId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<WebhookFailureAnomalyDetectionResult>.Filter.Eq(result => result.SubscriptionId, subscriptionId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default)
        => ToListAsync(Builders<WebhookFailureAnomalyDetectionResult>.Filter.Eq(result => result.EndpointId, endpointId), cancellationToken: cancellationToken);

    public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(Array.Empty<WebhookFailureAnomalyDetectionResult>());
        return ToListAsync(Builders<WebhookFailureAnomalyDetectionResult>.Filter.Empty, Builders<WebhookFailureAnomalyDetectionResult>.Sort.Descending(result => result.CalculatedAtUtc), limit, cancellationToken);
    }

    public Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetAnomaliesAsync(AiRiskLevel? minimumRiskLevel = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Task.FromResult<IReadOnlyList<WebhookFailureAnomalyDetectionResult>>(Array.Empty<WebhookFailureAnomalyDetectionResult>());
        var filter = Builders<WebhookFailureAnomalyDetectionResult>.Filter.Eq(result => result.IsAnomalyDetected, true);
        if (minimumRiskLevel is not null)
        {
            var levels = AllowedRiskLevels(minimumRiskLevel.Value);
            filter &= Builders<WebhookFailureAnomalyDetectionResult>.Filter.In(result => result.RiskLevel, levels);
        }
        return ToListAsync(filter, Builders<WebhookFailureAnomalyDetectionResult>.Sort.Descending(result => result.CalculatedAtUtc), limit, cancellationToken);
    }

    private static IReadOnlyList<string> AllowedRiskLevels(AiRiskLevel minimumRiskLevel)
    {
        var ordered = new[] { AiRiskLevel.Low, AiRiskLevel.Medium, AiRiskLevel.High, AiRiskLevel.Critical };
        var index = Array.IndexOf(ordered, minimumRiskLevel);
        return index < 0 ? ordered.Select(level => level.ToString()).ToArray() : ordered.Skip(index).Select(level => level.ToString()).ToArray();
    }

    private async Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> ToListAsync(FilterDefinition<WebhookFailureAnomalyDetectionResult> filter, SortDefinition<WebhookFailureAnomalyDetectionResult>? sort = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var options = new FindOptions<WebhookFailureAnomalyDetectionResult> { Sort = sort, Limit = limit };
        var cursor = await _collection.FindAsync(filter, options, cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
