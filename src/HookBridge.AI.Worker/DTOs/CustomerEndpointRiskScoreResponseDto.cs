namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Deterministic risk score output for a customer webhook endpoint.
/// </summary>
public sealed class CustomerEndpointRiskScoreResponseDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? TargetUrl { get; set; }
    public string? Environment { get; set; }
    public int RiskScore { get; set; }
    public AiRiskLevel RiskLevel { get; set; } = AiRiskLevel.Unknown;
    public EndpointHealthStatus HealthStatus { get; set; } = EndpointHealthStatus.Unknown;
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<CustomerEndpointRiskFactorDto> RiskFactors { get; set; } = [];
    public DateTime CalculatedAtUtc { get; set; }
}
