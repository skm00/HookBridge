using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class RetryAgentResultCollectionProvider : IRetryAgentResultCollectionProvider
{
    private readonly IMongoCollection<RetryAgentResult> _collection;
    public RetryAgentResultCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<RetryAgentResult>(options.Value.RetryAgentResultsCollectionName);
    public IMongoCollection<RetryAgentResult> GetCollection() => _collection;
}
