using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAnomalyRecordCollectionProvider : IAiAnomalyRecordCollectionProvider
{
    private readonly IMongoCollection<AiAnomalyRecord> _collection;

    public AiAnomalyRecordCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<AiAnomalyRecord>(mongoOptions.AiAnomalyRecordsCollectionName);
    }

    public IMongoCollection<AiAnomalyRecord> GetCollection() => _collection;
}
