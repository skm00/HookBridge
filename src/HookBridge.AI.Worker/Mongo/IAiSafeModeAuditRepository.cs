using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiSafeModeAuditRepository
{
    Task InsertAsync(AiSafeModeAuditRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSafeModeAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSafeModeAuditRecord>> SearchAsync(AiSafeModeAuditSearchRequestDto request, CancellationToken cancellationToken = default);
}
