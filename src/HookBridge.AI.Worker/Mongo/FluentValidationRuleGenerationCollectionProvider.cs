using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class FluentValidationRuleGenerationCollectionProvider : IFluentValidationRuleGenerationCollectionProvider
{
    private readonly IMongoCollection<FluentValidationRuleGenerationResult> _collection;

    public FluentValidationRuleGenerationCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        _collection = mongoClient.GetDatabase(mongoOptions.DatabaseName).GetCollection<FluentValidationRuleGenerationResult>(mongoOptions.FluentValidationRuleGenerationResultsCollectionName);
    }

    public IMongoCollection<FluentValidationRuleGenerationResult> GetCollection() => _collection;
}
