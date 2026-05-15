using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface ISecurityAgentResultRepository
{
    Task InsertAsync(SecurityAgentResult result, CancellationToken cancellationToken = default);
    Task<SecurityAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAgentResult>> SearchAsync(SecurityAgentSearchRequestDto request, CancellationToken cancellationToken = default);
}
