using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IAiDecisionEventProducer
{
    Task<AiKafkaPublishResult> PublishAsync(AiDecisionEventDto decisionEvent, CancellationToken cancellationToken = default);
}
