using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookTransformationRecommendationRepository : IWebhookTransformationRecommendationRepository
{
    private readonly IMongoCollection<WebhookTransformationRecommendationResult> _collection;
    public WebhookTransformationRecommendationRepository(IWebhookTransformationRecommendationCollectionProvider collectionProvider) => _collection = collectionProvider.GetCollection();
    public Task InsertAsync(WebhookTransformationRecommendationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }
}
