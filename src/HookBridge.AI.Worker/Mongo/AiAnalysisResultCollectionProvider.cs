using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAnalysisResultCollectionProvider : IAiAnalysisResultCollectionProvider
{
    private readonly IMongoCollection<AiAnalysisResult> _collection;

    public AiAnalysisResultCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<AiAnalysisResult>(mongoOptions.AiAnalysisResultsCollectionName);
    }

    public IMongoCollection<AiAnalysisResult> GetCollection() => _collection;
}
