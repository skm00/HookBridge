using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiAnalysisConsumer
{
    IAsyncEnumerable<AiAnalysisEventDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
