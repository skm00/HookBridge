using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IJsonToDtoSuggestionConsumer
{
    IAsyncEnumerable<JsonToDtoSuggestionRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
