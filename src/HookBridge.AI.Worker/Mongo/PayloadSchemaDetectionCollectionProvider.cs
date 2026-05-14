using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class PayloadSchemaDetectionCollectionProvider : IPayloadSchemaDetectionCollectionProvider
{
    private readonly IMongoCollection<PayloadSchemaDetectionResult> _collection;

    public PayloadSchemaDetectionCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<PayloadSchemaDetectionResult>(mongoOptions.PayloadSchemaDetectionResultsCollectionName);
    }

    public IMongoCollection<PayloadSchemaDetectionResult> GetCollection() => _collection;
}
