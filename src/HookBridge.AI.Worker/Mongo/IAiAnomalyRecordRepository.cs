using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiAnomalyRecordRepository
{
    Task<AiAnomalyRecordRepositoryResult> InsertAsync(AiAnomalyRecord record, CancellationToken cancellationToken = default);
    Task<AiAnomalyRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AiAnomalyRecord?> GetByAnomalyIdAsync(string anomalyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAnomalyRecord>> SearchAsync(AiAnomalyRecordSearchRequestDto request, CancellationToken cancellationToken = default);
}
