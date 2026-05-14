using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IWebhookFailureAnomalyDetectionRepository
{
    Task InsertAsync(WebhookFailureAnomalyDetectionResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookFailureAnomalyDetectionResult>> GetAnomaliesAsync(AiRiskLevel? minimumRiskLevel = null, int limit = 100, CancellationToken cancellationToken = default);
}
