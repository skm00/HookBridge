using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiAgentOrchestrationCollectionProvider
{
    IMongoCollection<AiAgentOrchestrationResult> GetCollection();
}
