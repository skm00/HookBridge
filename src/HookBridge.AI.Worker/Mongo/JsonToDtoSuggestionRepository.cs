using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class JsonToDtoSuggestionRepository : IJsonToDtoSuggestionRepository
{
    private readonly IMongoCollection<JsonToDtoSuggestionResult> _collection;

    public JsonToDtoSuggestionRepository(IJsonToDtoSuggestionCollectionProvider collectionProvider)
    {
        _collection = collectionProvider.GetCollection();
    }

    public Task InsertAsync(JsonToDtoSuggestionResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.CreatedAtUtc = DateTime.SpecifyKind(result.CreatedAtUtc, DateTimeKind.Utc);
        return _collection.InsertOneAsync(result, cancellationToken: cancellationToken);
    }
}
