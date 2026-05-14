using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public interface IJsonToDtoSuggestionCollectionProvider
{
    IMongoCollection<JsonToDtoSuggestionResult> GetCollection();
}
