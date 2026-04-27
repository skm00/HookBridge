using HookBridge.Application.Messaging;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaProducerPlaceholder : IKafkaProducer
{
    public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Kafka producer is not implemented yet.");
    }
}
