using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Kafka;

public interface IWebhookDuplicateReplayDetectionConsumer
{
    IAsyncEnumerable<WebhookDuplicateReplayDetectionMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}

public sealed record WebhookDuplicateReplayDetectionMessage(WebhookDuplicateReplayDetectionRequestDto Request, Func<CancellationToken, Task> AcknowledgeAsync);
