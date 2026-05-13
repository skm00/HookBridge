namespace HookBridge.AI.Worker.Mongo;

public interface IAiAnalysisResultRepository
{
    Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default);

    Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
