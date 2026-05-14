using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiAnomalyProducer
{
    Task<AiKafkaPublishResult> PublishAsync(AiAnomalyEventDto anomalyEvent, CancellationToken cancellationToken = default);
}
