using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiAnomalyRecordCollectionProvider
{
    IMongoCollection<AiAnomalyRecord> GetCollection();
}
