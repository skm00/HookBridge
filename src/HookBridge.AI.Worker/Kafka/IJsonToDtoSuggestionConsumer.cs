namespace HookBridge.AI.Worker.Kafka;

public interface IJsonToDtoSuggestionConsumer
{
    IAsyncEnumerable<JsonToDtoSuggestionMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}
