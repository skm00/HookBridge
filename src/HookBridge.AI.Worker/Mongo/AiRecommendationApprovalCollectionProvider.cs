using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiRecommendationApprovalCollectionProvider : IAiRecommendationApprovalCollectionProvider
{
    private readonly IMongoCollection<AiRecommendationApproval> _collection;

    public AiRecommendationApprovalCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<AiRecommendationApproval>(mongoOptions.AiRecommendationApprovalsCollectionName);
    }

    public IMongoCollection<AiRecommendationApproval> GetCollection() => _collection;
}
