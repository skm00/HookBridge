using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class CustomerEndpointRiskScoreCollectionProvider : ICustomerEndpointRiskScoreCollectionProvider
{
    private readonly IMongoCollection<CustomerEndpointRiskScoreResult> _collection;

    public CustomerEndpointRiskScoreCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<CustomerEndpointRiskScoreResult>(mongoOptions.CustomerEndpointRiskScoreResultsCollectionName);
    }

    public IMongoCollection<CustomerEndpointRiskScoreResult> GetCollection() => _collection;
}
