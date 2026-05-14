using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiSecurityAnalysisCollectionProvider : IAiSecurityAnalysisCollectionProvider
{
    private readonly IMongoCollection<AiSecurityAnalysisResult> _collection;
    public AiSecurityAnalysisCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<AiSecurityAnalysisResult>(options.Value.AiSecurityAnalysisResultsCollectionName);
    }
    public IMongoCollection<AiSecurityAnalysisResult> GetCollection() => _collection;
}
