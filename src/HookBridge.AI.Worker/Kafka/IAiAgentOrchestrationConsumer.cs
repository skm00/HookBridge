using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiAgentOrchestrationConsumer
{
    IAsyncEnumerable<AiAgentOrchestrationRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
