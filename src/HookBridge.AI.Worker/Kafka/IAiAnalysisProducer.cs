using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiAnalysisProducer
{
    Task<AiAnalysisPublishResult> PublishAsync(
        AiAnalysisEventDto analysisEvent,
        CancellationToken cancellationToken = default);
}
