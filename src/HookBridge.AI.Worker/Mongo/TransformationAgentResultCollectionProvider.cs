using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class TransformationAgentResultCollectionProvider : ITransformationAgentResultCollectionProvider
{
    private readonly IMongoCollection<TransformationAgentResult> _collection;
    public TransformationAgentResultCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<TransformationAgentResult>(options.Value.TransformationAgentResultsCollectionName);
    public IMongoCollection<TransformationAgentResult> GetCollection() => _collection;
}
