using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiSecurityAnalysisConsumer
{
    IAsyncEnumerable<AiSecurityAnalysisRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
