using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IObservabilityAgentResultCollectionProvider
{
    IMongoCollection<ObservabilityAgentResult> GetCollection();
}
