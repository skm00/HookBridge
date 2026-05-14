using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class PayloadSchemaDetectionRepository : IPayloadSchemaDetectionRepository
{
    private readonly IMongoCollection<PayloadSchemaDetectionResult> _collection;

    public PayloadSchemaDetectionRepository(IPayloadSchemaDetectionCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(PayloadSchemaDetectionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }
}
