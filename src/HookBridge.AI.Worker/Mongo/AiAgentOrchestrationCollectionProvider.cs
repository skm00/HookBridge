using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAgentOrchestrationCollectionProvider : IAiAgentOrchestrationCollectionProvider
{
    private readonly IMongoCollection<AiAgentOrchestrationResult> _collection;

    public AiAgentOrchestrationCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<AiAgentOrchestrationResult>(options.Value.AiAgentOrchestrationResultsCollectionName);
    }

    public IMongoCollection<AiAgentOrchestrationResult> GetCollection() => _collection;
}
