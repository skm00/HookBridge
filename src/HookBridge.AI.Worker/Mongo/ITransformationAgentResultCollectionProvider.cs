using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface ITransformationAgentResultCollectionProvider
{
    IMongoCollection<TransformationAgentResult> GetCollection();
}
