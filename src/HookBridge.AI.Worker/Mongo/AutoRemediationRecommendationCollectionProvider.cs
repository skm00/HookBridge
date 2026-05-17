using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AutoRemediationRecommendationCollectionProvider : IAutoRemediationRecommendationCollectionProvider
{
    private readonly IMongoCollection<AutoRemediationRecommendationResult> _collection;
    public AutoRemediationRecommendationCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<AutoRemediationRecommendationResult>(options.Value.AutoRemediationRecommendationResultsCollectionName);
    public IMongoCollection<AutoRemediationRecommendationResult> GetCollection() => _collection;
}
