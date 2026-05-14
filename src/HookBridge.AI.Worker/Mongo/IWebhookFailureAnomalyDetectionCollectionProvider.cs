using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookFailureAnomalyDetectionCollectionProvider
{
    IMongoCollection<WebhookFailureAnomalyDetectionResult> GetCollection();
}
