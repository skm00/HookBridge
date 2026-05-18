using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiDecisionEventConsumer
{
    IAsyncEnumerable<AiDecisionEventDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
