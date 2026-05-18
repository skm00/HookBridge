using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiDecisionAuditRecordCollectionProvider : IAiDecisionAuditRecordCollectionProvider
{
    private readonly IMongoCollection<AiDecisionAuditRecord> _collection;

    public AiDecisionAuditRecordCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<AiDecisionAuditRecord>(options.Value.AiDecisionAuditRecordsCollectionName);

    public IMongoCollection<AiDecisionAuditRecord> GetCollection() => _collection;
}
