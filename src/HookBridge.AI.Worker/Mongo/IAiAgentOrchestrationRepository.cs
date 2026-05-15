using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiAgentOrchestrationRepository
{
    Task InsertAsync(AiAgentOrchestrationResult result, CancellationToken cancellationToken = default);
    Task<AiAgentOrchestrationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAgentOrchestrationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAgentOrchestrationResult>> SearchAsync(AiAgentOrchestrationSearchRequestDto request, CancellationToken cancellationToken = default);
}
