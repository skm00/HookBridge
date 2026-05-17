using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAutoRemediationRecommendationRepository
{
    Task InsertAsync(AutoRemediationRecommendationResult result, CancellationToken cancellationToken = default);
    Task<AutoRemediationRecommendationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutoRemediationRecommendationResult>> SearchAsync(AutoRemediationRecommendationSearchRequestDto request, CancellationToken cancellationToken = default);
}
