using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class ObservabilityAgentResultCollectionProvider : IObservabilityAgentResultCollectionProvider
{
    private readonly IMongoCollection<ObservabilityAgentResult> _collection;
    public ObservabilityAgentResultCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<ObservabilityAgentResult>(options.Value.ObservabilityAgentResultsCollectionName);
    public IMongoCollection<ObservabilityAgentResult> GetCollection() => _collection;
}
