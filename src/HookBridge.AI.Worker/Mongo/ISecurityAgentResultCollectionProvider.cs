using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface ISecurityAgentResultCollectionProvider
{
    IMongoCollection<SecurityAgentResult> GetCollection();
}
