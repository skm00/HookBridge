using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IRetryAgentResultCollectionProvider
{
    IMongoCollection<RetryAgentResult> GetCollection();
}
