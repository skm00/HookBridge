namespace HookBridge.AI.Worker.Kafka;

public interface IWebhookFailureAnomalyDetectionConsumer
{
    IAsyncEnumerable<WebhookFailureAnomalyDetectionMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}
