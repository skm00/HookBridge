using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface ISecurityAgentConsumer
{
    IAsyncEnumerable<SecurityAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
