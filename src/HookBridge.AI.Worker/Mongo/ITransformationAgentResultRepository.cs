using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface ITransformationAgentResultRepository
{
    Task InsertAsync(TransformationAgentResult result, CancellationToken cancellationToken = default);
    Task<TransformationAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransformationAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransformationAgentResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransformationAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransformationAgentResult>> SearchAsync(TransformationAgentSearchRequestDto request, CancellationToken cancellationToken = default);
}
