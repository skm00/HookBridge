using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IDeadLetterAiAnalysisRepository
{
    Task InsertAsync(DeadLetterAiAnalysisResult result, CancellationToken cancellationToken = default);
    Task<DeadLetterAiAnalysisResult?> GetByDeadLetterIdAsync(string deadLetterId, CancellationToken cancellationToken = default);
    Task<DeadLetterAiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeadLetterAiAnalysisResult>> SearchAsync(DeadLetterAiAnalysisSearchRequestDto request, CancellationToken cancellationToken = default);
}
