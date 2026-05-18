using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiDecisionAuditRepository
{
    Task InsertAsync(AiDecisionAuditRecord record, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDecisionAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDecisionAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDecisionAuditRecord>> SearchAsync(AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken = default);
}
