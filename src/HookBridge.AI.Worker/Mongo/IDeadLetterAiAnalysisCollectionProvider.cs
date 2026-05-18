using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IDeadLetterAiAnalysisCollectionProvider
{
    IMongoCollection<DeadLetterAiAnalysisResult> GetCollection();
}
