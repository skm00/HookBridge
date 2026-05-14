using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class WebhookTransformationRecommendationCollectionProvider : IWebhookTransformationRecommendationCollectionProvider
{
    private readonly IMongoCollection<WebhookTransformationRecommendationResult> _collection;
    public WebhookTransformationRecommendationCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        _collection = mongoClient.GetDatabase(mongoOptions.DatabaseName).GetCollection<WebhookTransformationRecommendationResult>(mongoOptions.WebhookTransformationRecommendationResultsCollectionName);
    }
    public IMongoCollection<WebhookTransformationRecommendationResult> GetCollection() => _collection;
}
