namespace HookBridge.Application.DTOs.AiDashboard;

/// <summary>
/// AI dashboard rollup metrics for a filtered UTC time window.
/// </summary>
public sealed class AiDashboardSummaryResponseDto
{
    public string? Environment { get; set; }
    public string? CustomerId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long TotalAiAnalyses { get; set; }
    public long TotalAnomalies { get; set; }
    public long TotalSecurityFindings { get; set; }
    public long TotalHighRiskEndpoints { get; set; }
    public long TotalRetryRecommendations { get; set; }
    public long TotalDeadLetterRecommendations { get; set; }
    public double AverageConfidenceScore { get; set; }
    public AiDashboardRiskDistributionDto RiskDistribution { get; set; } = new();
    public IReadOnlyList<AiDashboardDistributionItemDto> AnomalyTypeDistribution { get; set; } = [];
    public IReadOnlyList<AiDashboardDistributionItemDto> RetryActionDistribution { get; set; } = [];
    public IReadOnlyList<AiDashboardDistributionItemDto> EndpointHealthDistribution { get; set; } = [];
    public IReadOnlyList<AiDashboardRecentFindingDto> RecentFindings { get; set; } = [];
    public DateTime GeneratedAtUtc { get; set; }
}
