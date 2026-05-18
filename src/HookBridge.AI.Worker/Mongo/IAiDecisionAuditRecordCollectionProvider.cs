using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiDecisionAuditRecordCollectionProvider
{
    IMongoCollection<AiDecisionAuditRecord> GetCollection();
}
