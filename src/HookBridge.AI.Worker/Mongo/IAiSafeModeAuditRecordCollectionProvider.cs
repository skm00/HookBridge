using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiSafeModeAuditRecordCollectionProvider
{
    IMongoCollection<AiSafeModeAuditRecord> GetCollection();
}
