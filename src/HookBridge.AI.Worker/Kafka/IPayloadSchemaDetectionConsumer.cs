using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IPayloadSchemaDetectionConsumer
{
    IAsyncEnumerable<PayloadSchemaDetectionRequestDto> ConsumeAsync(CancellationToken cancellationToken = default);
}
