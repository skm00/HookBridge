using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiSecurityAnalysisCollectionProvider
{
    IMongoCollection<AiSecurityAnalysisResult> GetCollection();
}
