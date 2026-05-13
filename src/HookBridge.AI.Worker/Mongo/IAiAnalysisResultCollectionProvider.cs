using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiAnalysisResultCollectionProvider
{
    IMongoCollection<AiAnalysisResult> GetCollection();
}
