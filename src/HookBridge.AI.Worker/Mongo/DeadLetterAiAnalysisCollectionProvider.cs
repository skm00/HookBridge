using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class DeadLetterAiAnalysisCollectionProvider : IDeadLetterAiAnalysisCollectionProvider
{
    private readonly IMongoCollection<DeadLetterAiAnalysisResult> _collection;
    public DeadLetterAiAnalysisCollectionProvider(IMongoClient client, IOptions<AiMongoOptions> options)
        => _collection = client.GetDatabase(options.Value.DatabaseName).GetCollection<DeadLetterAiAnalysisResult>(options.Value.DeadLetterAiAnalysisResultsCollectionName);
    public IMongoCollection<DeadLetterAiAnalysisResult> GetCollection() => _collection;
}
