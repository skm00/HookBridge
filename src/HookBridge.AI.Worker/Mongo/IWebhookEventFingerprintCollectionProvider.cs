using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookEventFingerprintCollectionProvider
{
    IMongoCollection<WebhookEventFingerprint> GetCollection();
}
