using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IFluentValidationRuleGenerationCollectionProvider
{
    IMongoCollection<FluentValidationRuleGenerationResult> GetCollection();
}
