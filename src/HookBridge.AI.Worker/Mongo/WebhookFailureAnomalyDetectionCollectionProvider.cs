using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookFailureAnomalyDetectionCollectionProvider : IWebhookFailureAnomalyDetectionCollectionProvider
{
    private readonly IMongoCollection<WebhookFailureAnomalyDetectionResult> _collection;

    public WebhookFailureAnomalyDetectionCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<WebhookFailureAnomalyDetectionResult>(mongoOptions.WebhookFailureAnomalyDetectionResultsCollectionName);
    }

    public IMongoCollection<WebhookFailureAnomalyDetectionResult> GetCollection() => _collection;
}
