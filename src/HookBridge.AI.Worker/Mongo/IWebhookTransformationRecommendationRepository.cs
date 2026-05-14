namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookTransformationRecommendationRepository
{
    Task InsertAsync(WebhookTransformationRecommendationResult result, CancellationToken cancellationToken = default);
}
