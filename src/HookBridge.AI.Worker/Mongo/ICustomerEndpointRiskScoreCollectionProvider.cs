using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface ICustomerEndpointRiskScoreCollectionProvider
{
    IMongoCollection<CustomerEndpointRiskScoreResult> GetCollection();
}
