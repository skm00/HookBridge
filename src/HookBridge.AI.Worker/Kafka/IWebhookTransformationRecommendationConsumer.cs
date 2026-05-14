namespace HookBridge.AI.Worker.Kafka;

public interface IWebhookTransformationRecommendationConsumer
{
    IAsyncEnumerable<WebhookTransformationRecommendationMessage> ConsumeAsync(CancellationToken cancellationToken = default);
}
