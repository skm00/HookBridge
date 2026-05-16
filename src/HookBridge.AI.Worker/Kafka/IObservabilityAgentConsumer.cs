using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IObservabilityAgentConsumer
{
    IAsyncEnumerable<ObservabilityAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
