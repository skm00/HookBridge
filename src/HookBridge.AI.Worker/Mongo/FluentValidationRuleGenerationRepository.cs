namespace HookBridge.AI.Worker.Mongo;

public sealed class FluentValidationRuleGenerationRepository : IFluentValidationRuleGenerationRepository
{
    private readonly IFluentValidationRuleGenerationCollectionProvider _collectionProvider;

    public FluentValidationRuleGenerationRepository(IFluentValidationRuleGenerationCollectionProvider collectionProvider)
    {
        _collectionProvider = collectionProvider;
    }

    public Task InsertAsync(FluentValidationRuleGenerationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collectionProvider.GetCollection().InsertOneAsync(result, cancellationToken: cancellationToken);
    }
}
