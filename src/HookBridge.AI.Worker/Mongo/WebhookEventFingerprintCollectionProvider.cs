using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookEventFingerprintCollectionProvider : IWebhookEventFingerprintCollectionProvider
{
    private readonly IMongoCollection<WebhookEventFingerprint> _collection;

    public WebhookEventFingerprintCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<WebhookEventFingerprint>(options.Value.WebhookEventFingerprintsCollectionName);
    }

    public IMongoCollection<WebhookEventFingerprint> GetCollection() => _collection;
}
