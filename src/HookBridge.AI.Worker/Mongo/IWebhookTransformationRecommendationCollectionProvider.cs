using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookTransformationRecommendationCollectionProvider
{
    IMongoCollection<WebhookTransformationRecommendationResult> GetCollection();
}
