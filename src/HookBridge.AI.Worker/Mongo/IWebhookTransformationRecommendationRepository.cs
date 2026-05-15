namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookTransformationRecommendationRepository
{
    Task InsertAsync(WebhookTransformationRecommendationResult result, CancellationToken cancellationToken = default);
    Task<WebhookTransformationRecommendationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookTransformationRecommendationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookTransformationRecommendationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookTransformationRecommendationResult>> SearchAsync(WebhookTransformationRecommendationSearchRequestDto request, CancellationToken cancellationToken = default);
}
