namespace HookBridge.AI.Worker.Mongo;

public interface IJsonToDtoSuggestionRepository
{
    Task InsertAsync(JsonToDtoSuggestionResult result, CancellationToken cancellationToken = default);
}
