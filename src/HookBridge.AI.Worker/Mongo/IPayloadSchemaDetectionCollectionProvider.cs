using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IPayloadSchemaDetectionCollectionProvider
{
    IMongoCollection<PayloadSchemaDetectionResult> GetCollection();
}
