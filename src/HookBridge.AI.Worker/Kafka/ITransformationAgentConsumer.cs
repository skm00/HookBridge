using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface ITransformationAgentConsumer
{
    IAsyncEnumerable<TransformationAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
