using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class SecurityAgentResultCollectionProvider : ISecurityAgentResultCollectionProvider
{
    private readonly IMongoCollection<SecurityAgentResult> _collection;
    public SecurityAgentResultCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<SecurityAgentResult>(options.Value.SecurityAgentResultsCollectionName);
    public IMongoCollection<SecurityAgentResult> GetCollection() => _collection;
}
