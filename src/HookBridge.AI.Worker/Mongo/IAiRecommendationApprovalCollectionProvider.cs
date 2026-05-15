using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiRecommendationApprovalCollectionProvider
{
    IMongoCollection<AiRecommendationApproval> GetCollection();
}
