using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAutoRemediationRecommendationCollectionProvider
{
    IMongoCollection<AutoRemediationRecommendationResult> GetCollection();
}
