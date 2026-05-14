namespace HookBridge.AI.Worker.Mongo;

public interface IAiAnalysisResultRepository
{
    Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default);

    Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(0L);

    Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

    Task<IReadOnlyDictionary<string, long>> CountByRetryActionAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

    Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(0d);

    Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDashboardRecentFindingResult>>(Array.Empty<AiDashboardRecentFindingResult>());
}
