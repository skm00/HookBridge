using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiAnomalyConsumer
{
    IAsyncEnumerable<AiAnomalyEventDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
