using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiSecurityAnalysisRepository
{
    Task InsertAsync(AiSecurityAnalysisResult result, CancellationToken cancellationToken = default);
    Task<AiSecurityAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSecurityAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSecurityAnalysisResult>> SearchAsync(AiSecurityAnalysisSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());
    Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(0d);
    Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDashboardRecentFindingResult>>(Array.Empty<AiDashboardRecentFindingResult>());
}
