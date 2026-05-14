using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class FluentValidationRuleGenerationRepository : IFluentValidationRuleGenerationRepository
{
    private readonly IMongoCollection<FluentValidationRuleGenerationResult> _collection;

    public FluentValidationRuleGenerationRepository(IFluentValidationRuleGenerationCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(FluentValidationRuleGenerationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }
}
