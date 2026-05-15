using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IRetryAgentResultRepository
{
    Task InsertAsync(RetryAgentResult result, CancellationToken cancellationToken = default);
    Task<RetryAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetryAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetryAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetryAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetryAgentResult>> SearchAsync(RetryAgentSearchRequestDto request, CancellationToken cancellationToken = default);
}
