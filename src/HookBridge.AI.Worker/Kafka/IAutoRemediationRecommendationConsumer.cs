using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAutoRemediationRecommendationConsumer
{
    IAsyncEnumerable<AutoRemediationRecommendationRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
