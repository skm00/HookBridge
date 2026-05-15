using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IRetryAgentConsumer
{
    IAsyncEnumerable<RetryAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
