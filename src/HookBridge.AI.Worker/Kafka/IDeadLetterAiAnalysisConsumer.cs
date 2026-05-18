using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IDeadLetterAiAnalysisConsumer
{
    IAsyncEnumerable<DeadLetterAiAnalysisRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
