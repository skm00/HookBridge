namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Deterministic health score output for a webhook target endpoint.
/// </summary>
public sealed class EndpointHealthScoreResponseDto
{
    public string EndpointId { get; set; } = string.Empty;

    public string? SubscriptionId { get; set; }

    public string? CustomerId { get; set; }

    public string? TargetUrl { get; set; }

    public string? Environment { get; set; }

    public int HealthScore { get; set; }

    public EndpointHealthStatus HealthStatus { get; set; } = EndpointHealthStatus.Unknown;

    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;

    public string Summary { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;

    public DateTime CalculatedAtUtc { get; set; }
}
