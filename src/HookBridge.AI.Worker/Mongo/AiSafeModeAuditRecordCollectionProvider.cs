using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSafeModeAuditRecordCollectionProvider : IAiSafeModeAuditRecordCollectionProvider
{
    private readonly IMongoCollection<AiSafeModeAuditRecord> _collection;

    public AiSafeModeAuditRecordCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<AiSafeModeAuditRecord>(options.Value.AiSafeModeAuditRecordsCollectionName);

    public IMongoCollection<AiSafeModeAuditRecord> GetCollection() => _collection;
}
