using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class JsonToDtoSuggestionCollectionProvider : IJsonToDtoSuggestionCollectionProvider
{
    private readonly IMongoCollection<JsonToDtoSuggestionResult> _collection;

    public JsonToDtoSuggestionCollectionProvider(IMongoClient mongoClient, IOptions<AiMongoOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<JsonToDtoSuggestionResult>(mongoOptions.JsonToDtoSuggestionResultsCollectionName);
    }

    public IMongoCollection<JsonToDtoSuggestionResult> GetCollection() => _collection;
}
